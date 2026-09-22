using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The orientation catalogue — authoring surface, HR only. Participants never come here: they reach
/// content through their own enrollment (<c>api/employee-orientations</c>), which is also where the
/// assessment is served with the answer key stripped. Exposing this controller to participants would
/// hand them <c>IsCorrect</c> on every option straight from the authoring DTO.
/// </summary>
[ApiController]
[OrientationBusinessRules]
[Route("api/orientation-programs")]
[Authorize(Policy = "InternalOnly")]
public class OrientationProgramsController : ControllerBase
{
    private readonly IOrientationProgramService _service;
    private readonly IOrientationEnrollmentTriggerService _triggers;
    private readonly ICurrentUserService _currentUser;

    public OrientationProgramsController(
        IOrientationProgramService service,
        IOrientationEnrollmentTriggerService triggers,
        ICurrentUserService currentUser)
    {
        _service = service;
        _triggers = triggers;
        _currentUser = currentUser;
    }

    // =========================================================================
    // PROGRAM QUERIES
    // =========================================================================

    [HttpGet]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<PagedResult<OrientationProgramSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("all")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OrientationProgramSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<OrientationProgramDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("code/{programCode}")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<OrientationProgramDto?>> GetByCode(string programCode)
        => Ok(await _service.GetByProgramCodeAsync(programCode));

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OrientationProgramSummaryDto>>> GetByStatus(OrientationProgramStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("category/{categoryId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OrientationProgramSummaryDto>>> GetByCategory(Guid categoryId)
        => Ok(await _service.GetByCategoryAsync(categoryId));

    [HttpGet("type/{programType}")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OrientationProgramSummaryDto>>> GetByType(OrientationProgramType programType)
        => Ok(await _service.GetByTypeAsync(programType));

    [HttpGet("active")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OrientationProgramSummaryDto>>> GetActive()
        => Ok(await _service.GetActiveProgramsAsync());

    [HttpGet("organization-unit/{organizationUnitId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OrientationProgramSummaryDto>>> GetByOrganizationUnit(Guid organizationUnitId)
        => Ok(await _service.GetByOwnerOrganizationUnitAsync(organizationUnitId));

    // =========================================================================
    // PROGRAM CRUD + LIFECYCLE
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationProgramDto>> Create([FromBody] CreateOrientationProgramDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        var created = await _service.CreateAsync(dto, ctx.TenantId, ctx.UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationProgramDto>> Update(Guid id, [FromBody] UpdateOrientationProgramDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateAsync(dto, userId));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeOrientationProgramStatusDto dto)
    {
        if (id != dto.ProgramId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        await _service.ChangeStatusAsync(dto, userId);
        return Ok(new { message = $"Program status changed to {dto.NewStatus}." });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.OrientationAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // MODULES
    // =========================================================================

    [HttpGet("{id:guid}/modules")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OrientationModuleDto>>> GetModules(Guid id)
        => Ok(await _service.GetModulesAsync(id));

    [HttpPost("{id:guid}/modules")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationModuleDto>> AddModule(Guid id, [FromBody] CreateOrientationModuleDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        dto.ProgramId = id;
        return Ok(await _service.AddModuleAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [HttpPut("modules/{moduleId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationModuleDto>> UpdateModule(Guid moduleId, [FromBody] UpdateOrientationModuleDto dto)
    {
        if (moduleId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateModuleAsync(dto, userId));
    }

    [HttpDelete("modules/{moduleId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationAdminPolicy)]
    public async Task<IActionResult> DeleteModule(Guid moduleId)
    {
        await _service.DeleteModuleAsync(moduleId);
        return NoContent();
    }

    // =========================================================================
    // CONTENT ITEMS
    // =========================================================================

    [HttpGet("modules/{moduleId:guid}/content-items")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OrientationContentItemDto>>> GetContentItems(Guid moduleId)
        => Ok(await _service.GetContentItemsAsync(moduleId));

    [HttpPost("modules/{moduleId:guid}/content-items")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationContentItemDto>> AddContentItem(Guid moduleId, [FromBody] CreateOrientationContentItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        dto.ModuleId = moduleId;
        return Ok(await _service.AddContentItemAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [HttpPut("content-items/{itemId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationContentItemDto>> UpdateContentItem(Guid itemId, [FromBody] UpdateOrientationContentItemDto dto)
    {
        if (itemId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateContentItemAsync(dto, userId));
    }

    [HttpDelete("content-items/{itemId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationAdminPolicy)]
    public async Task<IActionResult> DeleteContentItem(Guid itemId)
    {
        await _service.DeleteContentItemAsync(itemId);
        return NoContent();
    }

    // =========================================================================
    // PREREQUISITES
    // =========================================================================

    [HttpGet("{id:guid}/prerequisites")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OrientationPrerequisiteDto>>> GetPrerequisites(Guid id)
        => Ok(await _service.GetPrerequisitesAsync(id));

    [HttpPost("{id:guid}/prerequisites")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationPrerequisiteDto>> AddPrerequisite(Guid id, [FromBody] CreateOrientationPrerequisiteDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        dto.ProgramId = id;
        return Ok(await _service.AddPrerequisiteAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [HttpDelete("prerequisites/{prerequisiteId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationAdminPolicy)]
    public async Task<IActionResult> DeletePrerequisite(Guid prerequisiteId)
    {
        await _service.DeletePrerequisiteAsync(prerequisiteId);
        return NoContent();
    }

    // =========================================================================
    // AUDIENCE RULES
    // =========================================================================

    [HttpGet("{id:guid}/audience-rules")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OrientationAudienceRuleDto>>> GetAudienceRules(Guid id)
        => Ok(await _service.GetAudienceRulesAsync(id));

    [HttpPost("{id:guid}/audience-rules")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationAudienceRuleDto>> AddAudienceRule(Guid id, [FromBody] CreateOrientationAudienceRuleDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        dto.ProgramId = id;
        return Ok(await _service.AddAudienceRuleAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [HttpPut("audience-rules/{ruleId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationAudienceRuleDto>> UpdateAudienceRule(Guid ruleId, [FromBody] UpdateOrientationAudienceRuleDto dto)
    {
        if (ruleId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateAudienceRuleAsync(dto, userId));
    }

    [HttpDelete("audience-rules/{ruleId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationAdminPolicy)]
    public async Task<IActionResult> DeleteAudienceRule(Guid ruleId)
    {
        await _service.DeleteAudienceRuleAsync(ruleId);
        return NoContent();
    }

    // =========================================================================
    // TRIGGERS — round 4, lane I
    // =========================================================================

    /// <summary>
    /// How many people a rule's target reaches, asked while the rule is being written — the
    /// "this rule reaches 412 people" line (I2). A POST because it carries a body, not because it
    /// changes anything.
    /// </summary>
    [HttpPost("audience-rules/reach")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<OrientationAudienceReachDto>> CountReach([FromBody] OrientationAudienceReachRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _triggers.CountReachAsync(dto));
    }

    /// <summary>
    /// HR's "enrol the audience now": the programme's rules that have no date to count from.
    /// <c>preview=true</c> writes nothing and says what it would do.
    /// </summary>
    [HttpPost("{id:guid}/enrol-audience")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationTriggerRunResultDto>> EnrolAudience(Guid id, [FromQuery] bool preview = false)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId)) return BadRequest("Your user could not be resolved.");
        return Ok(await _triggers.EnrolAudienceNowAsync(id, userId, preview));
    }

    /// <summary>Which rules would fire for this employee, and why (I5).</summary>
    [HttpGet("triggers/diagnose/{employeeId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<OrientationTriggerDiagnosisDto>> Diagnose(Guid employeeId)
        => Ok(await _triggers.DiagnoseAsync(employeeId));

    /// <summary>
    /// The nightly sweep, now, for this tenant — the same code path the hosted service runs.
    /// <c>preview=true</c> writes nothing.
    /// </summary>
    [HttpPost("triggers/run")]
    [Authorize(Policy = HrPermissions.OrientationAdminPolicy)]
    public async Task<ActionResult<OrientationTriggerRunResultDto>> RunTriggers([FromQuery] bool preview = false)
    {
        if (_currentUser.TenantId is not { } tenantId) return BadRequest("Tenant context could not be resolved.");
        Guid? userId = Guid.TryParse(_currentUser.UserId, out var u) ? u : null;
        return Ok(await _triggers.RunSweepForTenantAsync(tenantId, "Manual", userId, preview));
    }

    // =========================================================================
    // ASSESSMENT QUESTIONS
    // =========================================================================

    [HttpGet("{id:guid}/questions")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OrientationAssessmentQuestionDto>>> GetQuestions(Guid id)
        => Ok(await _service.GetQuestionsAsync(id));

    [HttpPost("{id:guid}/questions")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationAssessmentQuestionDto>> AddQuestion(Guid id, [FromBody] CreateOrientationAssessmentQuestionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        dto.ProgramId = id;
        return Ok(await _service.AddQuestionAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [HttpPut("questions/{questionId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OrientationAssessmentQuestionDto>> UpdateQuestion(Guid questionId, [FromBody] UpdateOrientationAssessmentQuestionDto dto)
    {
        if (questionId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        return Ok(await _service.UpdateQuestionAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [HttpDelete("questions/{questionId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationAdminPolicy)]
    public async Task<IActionResult> DeleteQuestion(Guid questionId)
    {
        await _service.DeleteQuestionAsync(questionId);
        return NoContent();
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private (Guid TenantId, Guid UserId) ResolveContext(out ActionResult? error)
    {
        error = null;
        if (_currentUser.TenantId is not { } tenantId)
        {
            error = BadRequest("Tenant context could not be resolved.");
            return default;
        }
        if (_currentUser.EmployeeId is not { } userId)
        {
            error = BadRequest("Your user account is not linked to an employee record.");
            return default;
        }
        return (tenantId, userId);
    }
}
