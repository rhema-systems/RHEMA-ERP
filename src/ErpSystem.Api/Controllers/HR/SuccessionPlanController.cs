using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/succession-plans")]
[Authorize(Policy = HrPermissions.SuccessionReadPolicy)]
public class SuccessionPlanController : ControllerBase
{
    private readonly ISuccessionPlanService _service;
    private readonly ICurrentUserService _currentUser;

    public SuccessionPlanController(ISuccessionPlanService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<PagedResult<SuccessionPlanSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SuccessionPlanDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{planNumber}")]
    public async Task<ActionResult<SuccessionPlanDto?>> GetByPlanNumber(string planNumber)
        => Ok(await _service.GetByPlanNumberAsync(planNumber));

    [HttpGet("position/{positionId:guid}/active")]
    public async Task<ActionResult<SuccessionPlanDto?>> GetActiveVersionForPosition(Guid positionId)
        => Ok(await _service.GetActiveVersionForPositionAsync(positionId));

    [HttpGet("position/{positionId:guid}/versions")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetAllVersionsForPosition(Guid positionId)
        => Ok(await _service.GetAllVersionsForPositionAsync(positionId));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetByStatus(SuccessionPlanStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("year/{planYear:int}")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetByYear(int planYear)
        => Ok(await _service.GetByYearAsync(planYear));

    [HttpGet("year/{planYear:int}/status/{status}")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetByYearAndStatus(int planYear, SuccessionPlanStatus status)
        => Ok(await _service.GetByYearAndStatusAsync(planYear, status));

    [HttpGet("criticality/{criticality}")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetByCriticality(PositionCriticality criticality)
        => Ok(await _service.GetByCriticalityAsync(criticality));

    [HttpGet("risk/{riskLevel}")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetByRiskLevel(SuccessionRisk riskLevel)
        => Ok(await _service.GetByRiskLevelAsync(riskLevel));

    [HttpGet("due-for-review")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetDueForReview([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetDueForReviewAsync(daysAhead));

    [HttpGet("no-ready-now-successor")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetWithNoReadyNowSuccessor()
        => Ok(await _service.GetWithNoReadyNowSuccessorAsync());

    [HttpGet("no-successors")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetWithNoSuccessors()
        => Ok(await _service.GetWithNoSuccessorsAsync());

    [HttpGet("incumbent/{incumbentEmployeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetByIncumbent(Guid incumbentEmployeeId)
        => Ok(await _service.GetByIncumbentAsync(incumbentEmployeeId));

    [HttpGet("impending-vacancy")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanSummaryDto>>> GetWithImpendingVacancy([FromQuery] int daysAhead = 90)
        => Ok(await _service.GetWithImpendingVacancyAsync(daysAhead));

    /// <summary>
    /// The staff movements raised against this plan — whether the successor actually moved.
    /// </summary>
    /// <remarks>
    /// Read-only across the area-8 boundary. A movement already shows the plan it fulfils; this is
    /// the reverse, which was missing — a plan could name a successor and never show that anything
    /// came of it.
    /// </remarks>
    [HttpGet("{id:guid}/movements")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanMovementDto>>> GetMovements(Guid id)
        => Ok(await _service.GetMovementsAsync(id));

    [HttpGet("dashboard")]
    public async Task<ActionResult<SuccessionDashboardDto>> GetDashboard([FromQuery] int? planYear = null)
        => Ok(await _service.GetDashboardAsync(planYear));

    // =========================================================================
    // CRUD
    // =========================================================================

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<SuccessionPlanDto>> Create([FromBody] CreateSuccessionPlanDto dto)
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

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SuccessionPlanDto>> Update(Guid id, [FromBody] UpdateSuccessionPlanDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

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
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitForReview(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.SubmitForReviewAsync(id, employeeId.Value);
        return Ok(new { message = "Succession plan submitted for review." });
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    /// <summary>
    /// Rejects a plan that is out for approval.
    /// </summary>
    /// <remarks>
    /// Replaces the old <c>review</c> action, which took a caller-chosen <c>NewStatus</c> — a way
    /// to reach Approved without going through the approval the organisation configured.
    /// </remarks>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectSuccessionPlanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // The rejector is whoever is signed in. It used to arrive on the body.
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.PlanId = id;
        await _service.RejectAsync(dto, employeeId.Value);
        return Ok(new { message = "Succession plan rejected." });
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveSuccessionPlanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // The approver is whoever is signed in. It used to arrive on the body, so any caller could
        // approve a succession plan under a colleague's name.
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.PlanId = id;
        await _service.ApproveAsync(dto, employeeId.Value);
        return Ok(new { message = "Succession plan approved." });
    }

    // =========================================================================
    // COMPETENCY REQUIREMENTS
    // =========================================================================

    [HttpGet("competency-lookup")]
    public async Task<ActionResult<IEnumerable<CompetencyLookupDto>>> GetCompetencyLookup()
        => Ok(await _service.GetAllActiveCompetenciesAsync());

    [HttpGet("{id:guid}/competency-requirements")]
    public async Task<ActionResult<IEnumerable<SuccessionCompetencyRequirementDto>>> GetCompetencyRequirements(Guid id)
        => Ok(await _service.GetCompetencyRequirementsAsync(id));

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("{id:guid}/competency-requirements")]
    public async Task<ActionResult<SuccessionCompetencyRequirementDto>> AddCompetencyRequirement(
        Guid id, [FromBody] CreateSuccessionCompetencyRequirementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.SuccessionPlanId = id;
        var created = await _service.AddCompetencyRequirementAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetCompetencyRequirements), new { id }, created);
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPut("competency-requirements/{requirementId:guid}")]
    public async Task<ActionResult<SuccessionCompetencyRequirementDto>> UpdateCompetencyRequirement(
        Guid requirementId, [FromBody] UpdateSuccessionCompetencyRequirementDto dto)
    {
        if (requirementId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateCompetencyRequirementAsync(dto, employeeId.Value));
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpDelete("competency-requirements/{requirementId:guid}")]
    public async Task<IActionResult> DeleteCompetencyRequirement(Guid requirementId)
    {
        await _service.DeleteCompetencyRequirementAsync(requirementId);
        return NoContent();
    }

    // =========================================================================
    // ACTIONS
    // =========================================================================

    /// <summary>The plan's actions, in full — this read feeds a panel that edits them.</summary>
    [HttpGet("{id:guid}/actions")]
    public async Task<ActionResult<IEnumerable<SuccessionActionDto>>> GetActions(Guid id)
        => Ok(await _service.GetActionsForPlanAsync(id));

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("{id:guid}/actions")]
    public async Task<ActionResult<SuccessionActionDto>> AddAction(
        Guid id, [FromBody] CreateSuccessionActionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.SuccessionPlanId = id;
        try
        {
            var created = await _service.AddActionAsync(
                dto, tenantId.Value, employeeId.Value, assignedByEmployeeId: employeeId.Value);
            return CreatedAtAction(nameof(GetActions), new { id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPut("actions/{actionId:guid}")]
    public async Task<ActionResult<SuccessionActionDto>> UpdateAction(
        Guid actionId, [FromBody] UpdateSuccessionActionDto dto)
    {
        if (actionId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        try
        {
            return Ok(await _service.UpdateActionAsync(dto, employeeId.Value));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpDelete("actions/{actionId:guid}")]
    public async Task<IActionResult> DeleteAction(Guid actionId)
    {
        await _service.DeleteActionAsync(actionId);
        return NoContent();
    }

    // =========================================================================
    // HISTORY
    // =========================================================================

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IEnumerable<SuccessionPlanHistorySummaryDto>>> GetHistory(Guid id)
        => Ok(await _service.GetHistoryAsync(id));

    [HttpGet("{id:guid}/history/latest")]
    public async Task<ActionResult<SuccessionPlanHistoryDto?>> GetLatestSnapshot(Guid id)
        => Ok(await _service.GetLatestSnapshotAsync(id));

    // =========================================================================
    // DOCUMENTS
    // =========================================================================

    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<IEnumerable<SuccessionDocumentDto>>> GetDocuments(Guid id)
        => Ok(await _service.GetDocumentsForPlanAsync(id));

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpGet("{id:guid}/documents/confidential")]
    public async Task<ActionResult<IEnumerable<SuccessionDocumentDto>>> GetConfidentialDocuments(Guid id)
        => Ok(await _service.GetConfidentialDocumentsAsync(id));

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


        // D-14: a caller-supplied location let any HR user point a document row at arbitrary bytes
        // on disk. Files arrive through the upload route, which puts them past the scanner into
        // private storage; this route survives for the legacy migration utility and mints metadata
        // only. Same guard, same wording, as staff-movement attachments and provider documents.
        if (!string.IsNullOrWhiteSpace(dto.DocumentUrl) ||
            dto.FileUploadRecordId.HasValue ||
            dto.DocumentRecordId.HasValue ||
            dto.DocumentVersionId.HasValue)
        {
            return BadRequest(new
            {
                message = "File locations cannot be supplied directly. " +
                          "Use POST api/succession-documents/upload to attach a file."
            });
        }

        dto.SuccessionPlanId = id;
        var created = await _service.AddDocumentAsync(
            dto, tenantId.Value, employeeId.Value, uploadedByEmployeeId: employeeId.Value);
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
