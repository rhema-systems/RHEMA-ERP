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
/// Staff awards and recognition: the award catalogue, nominations, committee scoring, conferred
/// awards and long-service milestones.
/// </summary>
/// <remarks>
/// <para><b>Slice 1 took identity off the query string.</b> This controller shipped with sixty
/// caller-supplied id parameters — 24 <c>tenantId</c>, 32 <c>userId</c>, and four naming a domain
/// actor. Every one is now resolved from the token.</para>
///
/// <para>The tenant parameters were never a cross-tenant hole: a guard already refused a mismatched
/// tenant with a 403. They were removed because they made every caller responsible for supplying a
/// value the server already knew, and because a parameter that must always equal one particular
/// value is a trap rather than an input.</para>
///
/// <para><b>The actor parameters were a real hole, and it was confirmed by exploit.</b> Slice 0
/// created a nomination attributed to an employee who had nothing to do with the request. Three of
/// the four named an act performed by the caller — who nominated, who uploaded, who scored — and
/// those now come from the token, following the rule the module settled in area 13: <i>an act
/// performed by the caller at the moment of the call comes from the token; a fact about someone
/// else does not.</i> The fourth, the team member being added to a team nomination, is a fact about
/// someone else and stays in the payload.</para>
///
/// <para>Removing <c>nominatedById</c> also deleted a defect rather than guarding it. The parameter
/// was required, unknowable and unvalidated, so omitting it bound <c>Guid.Empty</c>, violated
/// <c>FK_AwardNominations_Employees_NominatedById</c> and surfaced as a generic 500 — the area-13
/// <c>FinalizedById</c> shape. There is nothing left to omit.</para>
///
/// <para><b>Gating.</b> Read on GET, Write on participation, Admin on catalogue administration
/// (types, levels, targets, budgets, committees) and on every delete. Nominating and voting are
/// deliberately NOT on this controller: they are acts of ordinary employees who hold no HR
/// permission, and they belong on the self-service surface. Gating them here would lock the
/// workforce out of the feature this area exists for — the area-15b trap.</para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AwardsController : HrControllerBase
{
    private readonly IAwardTypeService _awardTypeService;
    private readonly IAwardLevelService _awardLevelService;
    private readonly IAwardTypeTargetService _awardTypeTargetService;
    private readonly IAwardBudgetService _awardBudgetService;
    private readonly IEmployeeAwardService _employeeAwardService;
    private readonly IAwardAttachmentService _awardAttachmentService;
    private readonly IAwardNominationService _nominationService;
    private readonly ITeamAwardNomineeService _teamNomineeService;
    private readonly IAwardNomineeContributionService _nomineeContributionService;
    private readonly IAwardNominationAttachmentService _nominationAttachmentService;
    private readonly IAwardCommitteeService _committeeService;
    private readonly IAwardCommitteeMemberService _committeeMemberService;
    private readonly IAwardCommitteeReviewService _committeeReviewService;
    private readonly ILongServiceAwardService _longServiceAwardService;
    private readonly IAwardCycleService _cycleService;
    private readonly IAwardEligibilityService _eligibilityService;

    public AwardsController(
        IAwardTypeService awardTypeService,
        IAwardLevelService awardLevelService,
        IAwardTypeTargetService awardTypeTargetService,
        IAwardBudgetService awardBudgetService,
        IEmployeeAwardService employeeAwardService,
        IAwardAttachmentService awardAttachmentService,
        IAwardNominationService nominationService,
        ITeamAwardNomineeService teamNomineeService,
        IAwardNomineeContributionService nomineeContributionService,
        IAwardNominationAttachmentService nominationAttachmentService,
        IAwardCommitteeService committeeService,
        IAwardCommitteeMemberService committeeMemberService,
        IAwardCommitteeReviewService committeeReviewService,
        ILongServiceAwardService longServiceAwardService,
        IAwardCycleService cycleService,
        IAwardEligibilityService eligibilityService,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _awardTypeService = awardTypeService;
        _awardLevelService = awardLevelService;
        _awardTypeTargetService = awardTypeTargetService;
        _awardBudgetService = awardBudgetService;
        _employeeAwardService = employeeAwardService;
        _awardAttachmentService = awardAttachmentService;
        _nominationService = nominationService;
        _teamNomineeService = teamNomineeService;
        _nomineeContributionService = nomineeContributionService;
        _nominationAttachmentService = nominationAttachmentService;
        _committeeService = committeeService;
        _committeeMemberService = committeeMemberService;
        _committeeReviewService = committeeReviewService;
        _longServiceAwardService = longServiceAwardService;
        _cycleService = cycleService;
        _eligibilityService = eligibilityService;
    }

    #region Award Types

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<AwardTypeSummaryDto>>> GetAwardTypes()
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _awardTypeService.GetAllAsync(tenantId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/paged")]
    public async Task<ActionResult<PagedResult<AwardTypeSummaryDto>>> GetAwardTypesPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] AwardCategory? category = null)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _awardTypeService.GetPagedAsync(tenantId, pageNumber, pageSize, searchTerm, category);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{id:guid}")]
    public async Task<ActionResult<AwardTypeDto>> GetAwardType(Guid id)
    {
        var result = await _awardTypeService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{id:guid}/in-use")]
    public async Task<ActionResult<object>> GetAwardTypeInUse(Guid id)
    {
        var inUse = await _awardTypeService.IsInUseAsync(id);
        return Ok(new { inUse });
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/category/{category}")]
    public async Task<ActionResult<IEnumerable<AwardTypeSummaryDto>>> GetActiveAwardTypesByCategory(
        AwardCategory category)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _awardTypeService.GetActiveByCategoryAsync(tenantId, category);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPost("types")]
    public async Task<ActionResult<AwardTypeDto>> CreateAwardType(
        [FromBody] CreateAwardTypeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _awardTypeService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetAwardType), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPut("types/{id:guid}")]
    public async Task<ActionResult<AwardTypeDto>> UpdateAwardType(
        Guid id,
        [FromBody] UpdateAwardTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var updated = await _awardTypeService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("types/{id:guid}")]
    public async Task<IActionResult> DeleteAwardType(Guid id)
    {
        await _awardTypeService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Award Levels

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{awardTypeId:guid}/levels")]
    public async Task<ActionResult<IEnumerable<AwardLevelDto>>> GetAwardLevels(Guid awardTypeId)
    {
        var result = await _awardLevelService.GetByAwardTypeIdAsync(awardTypeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{awardTypeId:guid}/levels/active")]
    public async Task<ActionResult<IEnumerable<AwardLevelDto>>> GetActiveAwardLevels(Guid awardTypeId)
    {
        var result = await _awardLevelService.GetActiveByAwardTypeIdAsync(awardTypeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("levels/{id:guid}")]
    public async Task<ActionResult<AwardLevelDto>> GetAwardLevel(Guid id)
    {
        var result = await _awardLevelService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPost("levels")]
    public async Task<ActionResult<AwardLevelDto>> CreateAwardLevel(
        [FromBody] CreateAwardLevelDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _awardLevelService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetAwardLevel), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPut("levels/{id:guid}")]
    public async Task<ActionResult<AwardLevelDto>> UpdateAwardLevel(
        Guid id,
        [FromBody] UpdateAwardLevelDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var updated = await _awardLevelService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("levels/{id:guid}")]
    public async Task<IActionResult> DeleteAwardLevel(Guid id)
    {
        await _awardLevelService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Award Type Targets

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{awardTypeId:guid}/targets")]
    public async Task<ActionResult<IEnumerable<AwardTypeTargetDto>>> GetAwardTypeTargets(Guid awardTypeId)
    {
        var result = await _awardTypeTargetService.GetByAwardTypeIdAsync(awardTypeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{awardTypeId:guid}/targets/scope/{scope}")]
    public async Task<ActionResult<IEnumerable<AwardTypeTargetDto>>> GetAwardTypeTargetsByScope(
        Guid awardTypeId,
        AwardScope scope)
    {
        var result = await _awardTypeTargetService.GetByScopeAsync(awardTypeId, scope);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{awardTypeId:guid}/eligibility/{employeeId:guid}")]
    public async Task<ActionResult<bool>> CheckEmployeeEligibility(Guid awardTypeId, Guid employeeId)
    {
        var result = await _awardTypeTargetService.IsEmployeeEligibleAsync(awardTypeId, employeeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("targets/{id:guid}")]
    public async Task<ActionResult<AwardTypeTargetDto>> GetAwardTypeTarget(Guid id)
    {
        var result = await _awardTypeTargetService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPost("targets")]
    public async Task<ActionResult<AwardTypeTargetDto>> CreateAwardTypeTarget(
        [FromBody] CreateAwardTypeTargetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _awardTypeTargetService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetAwardTypeTarget), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPut("targets/{id:guid}")]
    public async Task<ActionResult<AwardTypeTargetDto>> UpdateAwardTypeTarget(
        Guid id,
        [FromBody] UpdateAwardTypeTargetDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var updated = await _awardTypeTargetService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("targets/{id:guid}")]
    public async Task<IActionResult> DeleteAwardTypeTarget(Guid id)
    {
        await _awardTypeTargetService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Award Budgets

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{awardTypeId:guid}/budgets")]
    public async Task<ActionResult<IEnumerable<AwardBudgetDto>>> GetAwardBudgets(Guid awardTypeId)
    {
        var result = await _awardBudgetService.GetByAwardTypeIdAsync(awardTypeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{awardTypeId:guid}/budgets/{year:int}")]
    public async Task<ActionResult<AwardBudgetDto>> GetAwardBudgetByYear(Guid awardTypeId, int year)
    {
        var result = await _awardBudgetService.GetByYearAsync(awardTypeId, year);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{awardTypeId:guid}/budgets/{year:int}/available")]
    public async Task<ActionResult<decimal>> GetAvailableBudget(Guid awardTypeId, int year)
    {
        var result = await _awardBudgetService.GetAvailableBudgetAsync(awardTypeId, year);
        return Ok(new { awardTypeId, year, availableAmount = result });
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("budgets/{id:guid}")]
    public async Task<ActionResult<AwardBudgetDto>> GetAwardBudget(Guid id)
    {
        var result = await _awardBudgetService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("budgets/year-range")]
    public async Task<ActionResult<IEnumerable<AwardBudgetDto>>> GetBudgetsByYearRange(
        [FromQuery] int startYear,
        [FromQuery] int endYear)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _awardBudgetService.GetByYearRangeAsync(tenantId, startYear, endYear);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPost("budgets")]
    public async Task<ActionResult<AwardBudgetDto>> CreateAwardBudget(
        [FromBody] CreateAwardBudgetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _awardBudgetService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetAwardBudget), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPut("budgets/{id:guid}")]
    public async Task<ActionResult<AwardBudgetDto>> UpdateAwardBudget(
        Guid id,
        [FromBody] UpdateAwardBudgetDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var updated = await _awardBudgetService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("budgets/{id:guid}")]
    public async Task<IActionResult> DeleteAwardBudget(Guid id)
    {
        await _awardBudgetService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Employee Awards

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmployeeAwardSummaryDto>>> GetEmployeeAwards()
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _employeeAwardService.GetAllAsync(tenantId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<EmployeeAwardSummaryDto>>> GetEmployeeAwardsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? year = null,
        [FromQuery] AwardStatus? status = null)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _employeeAwardService.GetPagedAsync(tenantId, pageNumber, pageSize, searchTerm, year, status);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeAwardDto>> GetEmployeeAward(Guid id)
    {
        var result = await _employeeAwardService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<EmployeeAwardDetailDto>> GetEmployeeAwardDetails(Guid id)
    {
        var result = await _employeeAwardService.GetWithDetailsAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeAwardSummaryDto>>> GetAwardsByEmployee(Guid employeeId)
    {
        var result = await _employeeAwardService.GetByEmployeeIdAsync(employeeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("pending/presentations")]
    public async Task<ActionResult<IEnumerable<EmployeeAwardSummaryDto>>> GetPendingPresentations()
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _employeeAwardService.GetPendingPresentationsAsync(tenantId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<EmployeeAwardDto>> CreateEmployeeAward(
        [FromBody] CreateEmployeeAwardDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _employeeAwardService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetEmployeeAward), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeAwardDto>> UpdateEmployeeAward(
        Guid id,
        [FromBody] UpdateEmployeeAwardDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var updated = await _employeeAwardService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteEmployeeAward(Guid id)
    {
        await _employeeAwardService.DeleteAsync(id);
        return NoContent();
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost("{id:guid}/presentation")]
    public async Task<IActionResult> SchedulePresentation(
        Guid id,
        [FromBody] ScheduleAwardPresentationDto dto)
    {
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        dto.AwardId = id;
        await _employeeAwardService.SchedulePresentationAsync(userId, dto);
        return Ok(new { message = "Presentation scheduled" });
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost("{id:guid}/payment")]
    public async Task<IActionResult> ProcessAwardPayment(
        Guid id,
        [FromBody] ProcessAwardPaymentDto dto)
    {
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        dto.AwardId = id;
        await _employeeAwardService.ProcessPaymentAsync(userId, dto);
        return Ok(new { message = "Payment processed" });
    }

    #endregion

    #region Award Attachments

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("{awardId:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<AwardAttachmentDto>>> GetAwardAttachments(Guid awardId)
    {
        var result = await _awardAttachmentService.GetByAwardIdAsync(awardId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("attachments/{id:guid}")]
    public async Task<ActionResult<AwardAttachmentDto>> GetAwardAttachment(Guid id)
    {
        var result = await _awardAttachmentService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost("{awardId:guid}/attachments")]
    public async Task<ActionResult<AwardAttachmentDto>> AddAwardAttachment(
        Guid awardId,
        [FromBody] CreateAwardAttachmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        dto.AwardId = awardId;
        var created = await _awardAttachmentService.CreateAsync(tenantId, awardId, userId, dto);
        return CreatedAtAction(nameof(GetAwardAttachment), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("attachments/{id:guid}")]
    public async Task<IActionResult> DeleteAwardAttachment(Guid id)
    {
        await _awardAttachmentService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Nominations

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("nominations")]
    public async Task<ActionResult<IEnumerable<AwardNominationSummaryDto>>> GetNominations()
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _nominationService.GetAllAsync(tenantId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("nominations/paged")]
    public async Task<ActionResult<PagedResult<AwardNominationSummaryDto>>> GetNominationsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? year = null,
        [FromQuery] AwardNominationStatus? status = null)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _nominationService.GetPagedAsync(tenantId, pageNumber, pageSize, searchTerm, year, status);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("nominations/{id:guid}")]
    public async Task<ActionResult<AwardNominationDto>> GetNomination(Guid id)
    {
        var result = await _nominationService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("nominations/nominee/{nomineeId:guid}")]
    public async Task<ActionResult<IEnumerable<AwardNominationSummaryDto>>> GetNominationsByNominee(Guid nomineeId)
    {
        var result = await _nominationService.GetByNomineeIdAsync(nomineeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost("nominations")]
    public async Task<ActionResult<AwardNominationDto>> CreateNomination(
        [FromBody] CreateAwardNominationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var nominatedById, "Raising a nomination") is { } error) return error;

        var created = await _nominationService.CreateAsync(tenantId, nominatedById, userId, dto);
        return CreatedAtAction(nameof(GetNomination), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPut("nominations/{id:guid}")]
    public async Task<ActionResult<AwardNominationDto>> UpdateNomination(
        Guid id,
        [FromBody] UpdateAwardNominationDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var updated = await _nominationService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("nominations/{id:guid}")]
    public async Task<IActionResult> DeleteNomination(Guid id)
    {
        await _nominationService.DeleteAsync(id);
        return NoContent();
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost("nominations/{id:guid}/submit")]
    public async Task<ActionResult<AwardNominationDto>> SubmitNomination(Guid id)
    {
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var updated = await _nominationService.SubmitAsync(id, userId);
        return Ok(updated);
    }

    #endregion

    #region Team Award Nominees

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("nominations/{nominationId:guid}/team-nominees")]
    public async Task<ActionResult<IEnumerable<TeamAwardNomineeDto>>> GetTeamNominees(Guid nominationId)
    {
        var result = await _teamNomineeService.GetByNominationIdAsync(nominationId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("team-nominees/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TeamAwardNomineeDto>>> GetTeamNomineesByEmployee(Guid employeeId)
    {
        var result = await _teamNomineeService.GetByEmployeeIdAsync(employeeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost("nominations/{nominationId:guid}/team-nominees")]
    /// <remarks>
    /// The team member being added is a fact about somebody else, so it stays in the payload — it is
    /// not the caller and must not come from the token. It previously arrived twice: once as a query
    /// parameter and once as <see cref="CreateTeamAwardNomineeDto.EmployeeId"/>, which is
    /// <c>[Required]</c> and which the mapper discarded. Fill the required field correctly and omit
    /// the parameter and you got <c>Guid.Empty</c>, a foreign-key violation and a 500. One source
    /// now: the DTO.
    /// </remarks>
    public async Task<ActionResult<TeamAwardNomineeDto>> AddTeamNominee(
        Guid nominationId,
        [FromBody] CreateTeamAwardNomineeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var created = await _teamNomineeService.AddAsync(nominationId, dto.EmployeeId, userId, dto);
        return Ok(created);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPut("team-nominees/{id:guid}")]
    public async Task<IActionResult> UpdateTeamNominee(
        Guid id,
        [FromBody] UpdateTeamAwardNomineeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        await _teamNomineeService.UpdateAsync(id, userId, dto);
        return Ok(new { message = "Team nominee updated" });
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("team-nominees/{id:guid}")]
    public async Task<IActionResult> RemoveTeamNominee(Guid id)
    {
        await _teamNomineeService.RemoveAsync(id);
        return NoContent();
    }

    #endregion

    #region Nominee Contributions

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("nominations/{nominationId:guid}/contributions")]
    public async Task<ActionResult<IEnumerable<AwardNomineeContributionDto>>> GetNomineeContributions(Guid nominationId)
    {
        var result = await _nomineeContributionService.GetByNominationIdAsync(nominationId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost("nominations/{nominationId:guid}/contributions")]
    public async Task<ActionResult<AwardNomineeContributionDto>> AddNomineeContribution(
        Guid nominationId,
        [FromBody] CreateAwardNomineeContributionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var created = await _nomineeContributionService.AddAsync(nominationId, userId, dto);
        return Ok(created);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPut("contributions/{id:guid}")]
    public async Task<IActionResult> UpdateNomineeContribution(
        Guid id,
        [FromBody] UpdateAwardNomineeContributionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        await _nomineeContributionService.UpdateAsync(id, userId, dto);
        return Ok(new { message = "Contribution updated" });
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("contributions/{id:guid}")]
    public async Task<IActionResult> RemoveNomineeContribution(Guid id)
    {
        await _nomineeContributionService.RemoveAsync(id);
        return NoContent();
    }

    #endregion

    #region Nomination Attachments

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("nominations/{nominationId:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<AwardNominationAttachmentDto>>> GetNominationAttachments(Guid nominationId)
    {
        var result = await _nominationAttachmentService.GetByNominationIdAsync(nominationId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost("nominations/{nominationId:guid}/attachments")]
    public async Task<ActionResult<AwardNominationAttachmentDto>> AddNominationAttachment(
        Guid nominationId,
        [FromBody] CreateAwardNominationAttachmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out var userId, out var uploadedById, "Attaching a document to a nomination") is { } error) return error;

        var created = await _nominationAttachmentService.AddAsync(nominationId, uploadedById, userId, dto);
        return Ok(created);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPut("nomination-attachments/{id:guid}")]
    public async Task<IActionResult> UpdateNominationAttachment(
        Guid id,
        [FromBody] UpdateAwardNominationAttachmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        await _nominationAttachmentService.UpdateAsync(id, userId, dto);
        return Ok(new { message = "Attachment updated" });
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("nomination-attachments/{id:guid}")]
    public async Task<IActionResult> RemoveNominationAttachment(Guid id)
    {
        await _nominationAttachmentService.RemoveAsync(id);
        return NoContent();
    }

    #endregion

    #region Award Committees

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("committees")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeDto>>> GetCommittees()
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _committeeService.GetAllAsync(tenantId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("committees/active")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeDto>>> GetActiveCommittees()
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _committeeService.GetActiveCommitteesAsync(tenantId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("committees/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeDto>> GetCommittee(Guid id)
    {
        var result = await _committeeService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("committees/{id:guid}/with-members")]
    public async Task<ActionResult<AwardCommitteeDto>> GetCommitteeWithMembers(Guid id)
    {
        var result = await _committeeService.GetWithMembersAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("committees/{id:guid}/has-quorum")]
    public async Task<ActionResult<bool>> CheckCommitteeQuorum(Guid id)
    {
        var result = await _committeeService.HasQuorumAsync(id);
        return Ok(new { committeeId = id, hasQuorum = result });
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPost("committees")]
    public async Task<ActionResult<AwardCommitteeDto>> CreateCommittee(
        [FromBody] CreateAwardCommitteeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _committeeService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetCommittee), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPut("committees/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeDto>> UpdateCommittee(
        Guid id,
        [FromBody] UpdateAwardCommitteeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var updated = await _committeeService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("committees/{id:guid}")]
    public async Task<IActionResult> DeleteCommittee(Guid id)
    {
        await _committeeService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Award Committee Members

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("committees/{committeeId:guid}/members")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeMemberDto>>> GetCommitteeMembers(Guid committeeId)
    {
        var result = await _committeeMemberService.GetByCommitteeIdAsync(committeeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("committees/{committeeId:guid}/members/active")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeMemberDto>>> GetActiveCommitteeMembers(Guid committeeId)
    {
        var result = await _committeeMemberService.GetActiveByCommitteeIdAsync(committeeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("committee-members/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeMemberDto>> GetCommitteeMember(Guid id)
    {
        var result = await _committeeMemberService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("committee-members/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeMemberDto>>> GetMembershipsByEmployee(Guid employeeId)
    {
        var result = await _committeeMemberService.GetByEmployeeIdAsync(employeeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("committees/{committeeId:guid}/members/{employeeId:guid}/is-active")]
    public async Task<ActionResult<bool>> CheckActiveMembership(Guid committeeId, Guid employeeId)
    {
        var result = await _committeeMemberService.IsActiveMemberAsync(committeeId, employeeId);
        return Ok(new { committeeId, employeeId, isActiveMember = result });
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPost("committees/{committeeId:guid}/members")]
    public async Task<ActionResult<AwardCommitteeMemberDto>> AddCommitteeMember(
        Guid committeeId,
        [FromBody] CreateAwardCommitteeMemberDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var created = await _committeeMemberService.AddAsync(committeeId, userId, dto);
        return CreatedAtAction(nameof(GetCommitteeMember), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPut("committee-members/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeMemberDto>> UpdateCommitteeMember(
        Guid id,
        [FromBody] UpdateAwardCommitteeMemberDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var updated = await _committeeMemberService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("committee-members/{id:guid}")]
    public async Task<IActionResult> RemoveCommitteeMember(Guid id)
    {
        await _committeeMemberService.RemoveAsync(id);
        return NoContent();
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPost("committee-members/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateCommitteeMember(
        Guid id,
        [FromQuery] DateTime? endDate = null)
    {
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        await _committeeMemberService.DeactivateAsync(id, userId, endDate);
        return Ok(new { message = "Committee member deactivated" });
    }

    #endregion

    #region Award Committee Reviews

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("nominations/{nominationId:guid}/reviews")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeReviewDto>>> GetNominationReviews(Guid nominationId)
    {
        var result = await _committeeReviewService.GetByNominationIdAsync(nominationId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("nominations/{nominationId:guid}/reviews/approval-count")]
    public async Task<ActionResult<int>> GetApprovalCount(Guid nominationId)
    {
        var result = await _committeeReviewService.GetApprovalCountAsync(nominationId);
        return Ok(new { nominationId, approvalCount = result });
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("nominations/{nominationId:guid}/reviews/rejection-count")]
    public async Task<ActionResult<int>> GetRejectionCount(Guid nominationId)
    {
        var result = await _committeeReviewService.GetRejectionCountAsync(nominationId);
        return Ok(new { nominationId, rejectionCount = result });
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("reviews/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeReviewDto>> GetReview(Guid id)
    {
        var result = await _committeeReviewService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("reviews/reviewer/{reviewerId:guid}")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeReviewDto>>> GetReviewsByReviewer(Guid reviewerId)
    {
        var result = await _committeeReviewService.GetByReviewerIdAsync(reviewerId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("reviews/reviewer/{reviewerId:guid}/pending")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeReviewDto>>> GetPendingReviews(Guid reviewerId)
    {
        var result = await _committeeReviewService.GetPendingReviewsAsync(reviewerId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost("nominations/{nominationId:guid}/reviews")]
    public async Task<ActionResult<AwardCommitteeReviewDto>> SubmitReview(
        Guid nominationId,
        [FromBody] SubmitCommitteeReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out var userId, out var reviewerId, "Scoring a nomination") is { } error) return error;

        var created = await _committeeReviewService.SubmitReviewAsync(nominationId, reviewerId, userId, dto);
        return Ok(created);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPut("reviews/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeReviewDto>> UpdateReview(
        Guid id,
        [FromBody] UpdateCommitteeReviewDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var updated = await _committeeReviewService.UpdateReviewAsync(id, userId, dto);
        return Ok(updated);
    }

    #endregion

    #region Long Service Awards

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("long-service")]
    public async Task<ActionResult<IEnumerable<LongServiceAwardSummaryDto>>> GetLongServiceAwards()
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _longServiceAwardService.GetAllAsync(tenantId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("long-service/paged")]
    public async Task<ActionResult<PagedResult<LongServiceAwardSummaryDto>>> GetLongServiceAwardsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? yearsOfService = null)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _longServiceAwardService.GetPagedAsync(tenantId, pageNumber, pageSize, searchTerm, yearsOfService);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("long-service/{id:guid}")]
    public async Task<ActionResult<LongServiceAwardDto>> GetLongServiceAward(Guid id)
    {
        var result = await _longServiceAwardService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("long-service/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<LongServiceAwardSummaryDto>>> GetLongServiceByEmployee(Guid employeeId)
    {
        var result = await _longServiceAwardService.GetByEmployeeIdAsync(employeeId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("long-service/upcoming")]
    public async Task<ActionResult<IEnumerable<LongServiceAwardSummaryDto>>> GetUpcomingMilestones(
        [FromQuery] int daysAhead = 90)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _longServiceAwardService.GetUpcomingMilestonesAsync(tenantId, daysAhead);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("long-service/pending-processing")]
    public async Task<ActionResult<IEnumerable<LongServiceAwardSummaryDto>>> GetPendingProcessing()
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var result = await _longServiceAwardService.GetPendingProcessingAsync(tenantId);
        return Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost("long-service")]
    public async Task<ActionResult<LongServiceAwardDto>> CreateLongServiceAward(
        [FromBody] CreateLongServiceAwardDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _longServiceAwardService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetLongServiceAward), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPut("long-service/{id:guid}")]
    public async Task<ActionResult<LongServiceAwardDto>> UpdateLongServiceAward(
        Guid id,
        [FromBody] UpdateLongServiceAwardDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        var updated = await _longServiceAwardService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("long-service/{id:guid}")]
    public async Task<IActionResult> DeleteLongServiceAward(Guid id)
    {
        await _longServiceAwardService.DeleteAsync(id);
        return NoContent();
    }

    [Authorize(Policy = HrPermissions.AwardsWritePolicy)]
    [HttpPost("long-service/{id:guid}/process")]
    public async Task<IActionResult> ProcessLongServiceAward(
        Guid id,
        [FromBody] ProcessLongServiceAwardDto dto)
    {
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        dto.AwardId = id;
        await _longServiceAwardService.ProcessAsync(userId, dto);
        return Ok(new { message = "Long service award processed" });
    }

    #endregion

    #region Award Cycles

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{awardTypeId:guid}/cycles")]
    public async Task<ActionResult<IEnumerable<AwardCycleSummaryDto>>> GetCycles(Guid awardTypeId)
        => Ok(await _cycleService.GetByAwardTypeIdAsync(awardTypeId));

    /// <summary>Cycles accepting nominations right now — derived from the clock, never from a stored flag.</summary>
    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("cycles/open-for-nomination")]
    public async Task<ActionResult<IEnumerable<AwardCycleSummaryDto>>> GetCyclesOpenForNomination()
        => Ok(await _cycleService.GetOpenForNominationAsync());

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("cycles/open-for-voting")]
    public async Task<ActionResult<IEnumerable<AwardCycleSummaryDto>>> GetCyclesOpenForVoting()
        => Ok(await _cycleService.GetOpenForVotingAsync());

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("cycles/{id:guid}")]
    public async Task<ActionResult<AwardCycleDto>> GetCycle(Guid id)
    {
        var result = await _cycleService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPost("cycles")]
    public async Task<ActionResult<AwardCycleDto>> CreateCycle([FromBody] CreateAwardCycleDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _cycleService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetCycle), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPut("cycles/{id:guid}")]
    public async Task<ActionResult<AwardCycleDto>> UpdateCycle(Guid id, [FromBody] UpdateAwardCycleDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _cycleService.UpdateAsync(id, userId, dto));
    }

    /// <summary>Takes a cycle out of draft. Re-checks the windows: the award's selection model may have changed since.</summary>
    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPost("cycles/{id:guid}/publish")]
    public async Task<ActionResult<AwardCycleDto>> PublishCycle(Guid id)
    {
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;
        return Ok(await _cycleService.PublishAsync(id, userId));
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpPost("cycles/{id:guid}/cancel")]
    public async Task<ActionResult<AwardCycleDto>> CancelCycle(Guid id, [FromBody] CancelAwardCycleDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;
        return Ok(await _cycleService.CancelAsync(id, userId, dto.Reason));
    }

    [Authorize(Policy = HrPermissions.AwardsAdminPolicy)]
    [HttpDelete("cycles/{id:guid}")]
    public async Task<IActionResult> DeleteCycle(Guid id)
    {
        await _cycleService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Eligibility

    /// <summary>
    /// Who qualifies for this award, and why anyone else does not.
    /// </summary>
    /// <remarks>
    /// This is the "management will set the criteria and then it will qualify some employees" step
    /// of TDC's note. The ineligible list is returned deliberately: without it, a mis-set rule and a
    /// correct one produce the same screen.
    /// </remarks>
    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{awardTypeId:guid}/eligible")]
    public async Task<ActionResult<AwardEligibilityResultDto>> GetEligible(
        Guid awardTypeId,
        [FromQuery] DateTime? asOf = null)
        => Ok(await _eligibilityService.EvaluateAsync(awardTypeId, asOf));

    [Authorize(Policy = HrPermissions.AwardsReadPolicy)]
    [HttpGet("types/{awardTypeId:guid}/eligible/{employeeId:guid}")]
    public async Task<ActionResult<AwardEligibilityVerdictDto>> GetEmployeeEligibility(
        Guid awardTypeId,
        Guid employeeId,
        [FromQuery] DateTime? asOf = null)
        => Ok(await _eligibilityService.EvaluateEmployeeAsync(awardTypeId, employeeId, asOf));

    #endregion

}

