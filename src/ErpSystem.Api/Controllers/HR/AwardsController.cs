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
public class AwardsController : ControllerBase
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
        ILongServiceAwardService longServiceAwardService)
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
    }

    #region Award Types

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<AwardTypeSummaryDto>>> GetAwardTypes([FromQuery] Guid tenantId)
    {
        var result = await _awardTypeService.GetAllAsync(tenantId);
        return Ok(result);
    }

    [HttpGet("types/paged")]
    public async Task<ActionResult<PagedResult<AwardTypeSummaryDto>>> GetAwardTypesPaged(
        [FromQuery] Guid tenantId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] AwardCategory? category = null)
    {
        var result = await _awardTypeService.GetPagedAsync(tenantId, pageNumber, pageSize, searchTerm, category);
        return Ok(result);
    }

    [HttpGet("types/{id:guid}")]
    public async Task<ActionResult<AwardTypeDto>> GetAwardType(Guid id)
    {
        var result = await _awardTypeService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("types/{id:guid}/in-use")]
    public async Task<ActionResult<object>> GetAwardTypeInUse(Guid id)
    {
        var inUse = await _awardTypeService.IsInUseAsync(id);
        return Ok(new { inUse });
    }

    [HttpGet("types/category/{category}")]
    public async Task<ActionResult<IEnumerable<AwardTypeSummaryDto>>> GetActiveAwardTypesByCategory(
        [FromQuery] Guid tenantId,
        AwardCategory category)
    {
        var result = await _awardTypeService.GetActiveByCategoryAsync(tenantId, category);
        return Ok(result);
    }

    [HttpPost("types")]
    public async Task<ActionResult<AwardTypeDto>> CreateAwardType(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid userId,
        [FromBody] CreateAwardTypeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _awardTypeService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetAwardType), new { id = created.Id }, created);
    }

    [HttpPut("types/{id:guid}")]
    public async Task<ActionResult<AwardTypeDto>> UpdateAwardType(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateAwardTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _awardTypeService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [HttpDelete("types/{id:guid}")]
    public async Task<IActionResult> DeleteAwardType(Guid id)
    {
        await _awardTypeService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Award Levels

    [HttpGet("types/{awardTypeId:guid}/levels")]
    public async Task<ActionResult<IEnumerable<AwardLevelDto>>> GetAwardLevels(Guid awardTypeId)
    {
        var result = await _awardLevelService.GetByAwardTypeIdAsync(awardTypeId);
        return Ok(result);
    }

    [HttpGet("types/{awardTypeId:guid}/levels/active")]
    public async Task<ActionResult<IEnumerable<AwardLevelDto>>> GetActiveAwardLevels(Guid awardTypeId)
    {
        var result = await _awardLevelService.GetActiveByAwardTypeIdAsync(awardTypeId);
        return Ok(result);
    }

    [HttpGet("levels/{id:guid}")]
    public async Task<ActionResult<AwardLevelDto>> GetAwardLevel(Guid id)
    {
        var result = await _awardLevelService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("levels")]
    public async Task<ActionResult<AwardLevelDto>> CreateAwardLevel(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid userId,
        [FromBody] CreateAwardLevelDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _awardLevelService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetAwardLevel), new { id = created.Id }, created);
    }

    [HttpPut("levels/{id:guid}")]
    public async Task<ActionResult<AwardLevelDto>> UpdateAwardLevel(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateAwardLevelDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _awardLevelService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [HttpDelete("levels/{id:guid}")]
    public async Task<IActionResult> DeleteAwardLevel(Guid id)
    {
        await _awardLevelService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Award Type Targets

    [HttpGet("types/{awardTypeId:guid}/targets")]
    public async Task<ActionResult<IEnumerable<AwardTypeTargetDto>>> GetAwardTypeTargets(Guid awardTypeId)
    {
        var result = await _awardTypeTargetService.GetByAwardTypeIdAsync(awardTypeId);
        return Ok(result);
    }

    [HttpGet("types/{awardTypeId:guid}/targets/scope/{scope}")]
    public async Task<ActionResult<IEnumerable<AwardTypeTargetDto>>> GetAwardTypeTargetsByScope(
        Guid awardTypeId,
        AwardScope scope)
    {
        var result = await _awardTypeTargetService.GetByScopeAsync(awardTypeId, scope);
        return Ok(result);
    }

    [HttpGet("types/{awardTypeId:guid}/eligibility/{employeeId:guid}")]
    public async Task<ActionResult<bool>> CheckEmployeeEligibility(Guid awardTypeId, Guid employeeId)
    {
        var result = await _awardTypeTargetService.IsEmployeeEligibleAsync(awardTypeId, employeeId);
        return Ok(result);
    }

    [HttpGet("targets/{id:guid}")]
    public async Task<ActionResult<AwardTypeTargetDto>> GetAwardTypeTarget(Guid id)
    {
        var result = await _awardTypeTargetService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("targets")]
    public async Task<ActionResult<AwardTypeTargetDto>> CreateAwardTypeTarget(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid userId,
        [FromBody] CreateAwardTypeTargetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _awardTypeTargetService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetAwardTypeTarget), new { id = created.Id }, created);
    }

    [HttpPut("targets/{id:guid}")]
    public async Task<ActionResult<AwardTypeTargetDto>> UpdateAwardTypeTarget(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateAwardTypeTargetDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _awardTypeTargetService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [HttpDelete("targets/{id:guid}")]
    public async Task<IActionResult> DeleteAwardTypeTarget(Guid id)
    {
        await _awardTypeTargetService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Award Budgets

    [HttpGet("types/{awardTypeId:guid}/budgets")]
    public async Task<ActionResult<IEnumerable<AwardBudgetDto>>> GetAwardBudgets(Guid awardTypeId)
    {
        var result = await _awardBudgetService.GetByAwardTypeIdAsync(awardTypeId);
        return Ok(result);
    }

    [HttpGet("types/{awardTypeId:guid}/budgets/{year:int}")]
    public async Task<ActionResult<AwardBudgetDto>> GetAwardBudgetByYear(Guid awardTypeId, int year)
    {
        var result = await _awardBudgetService.GetByYearAsync(awardTypeId, year);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("types/{awardTypeId:guid}/budgets/{year:int}/available")]
    public async Task<ActionResult<decimal>> GetAvailableBudget(Guid awardTypeId, int year)
    {
        var result = await _awardBudgetService.GetAvailableBudgetAsync(awardTypeId, year);
        return Ok(new { awardTypeId, year, availableAmount = result });
    }

    [HttpGet("budgets/{id:guid}")]
    public async Task<ActionResult<AwardBudgetDto>> GetAwardBudget(Guid id)
    {
        var result = await _awardBudgetService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("budgets/year-range")]
    public async Task<ActionResult<IEnumerable<AwardBudgetDto>>> GetBudgetsByYearRange(
        [FromQuery] Guid tenantId,
        [FromQuery] int startYear,
        [FromQuery] int endYear)
    {
        var result = await _awardBudgetService.GetByYearRangeAsync(tenantId, startYear, endYear);
        return Ok(result);
    }

    [HttpPost("budgets")]
    public async Task<ActionResult<AwardBudgetDto>> CreateAwardBudget(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid userId,
        [FromBody] CreateAwardBudgetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _awardBudgetService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetAwardBudget), new { id = created.Id }, created);
    }

    [HttpPut("budgets/{id:guid}")]
    public async Task<ActionResult<AwardBudgetDto>> UpdateAwardBudget(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateAwardBudgetDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _awardBudgetService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [HttpDelete("budgets/{id:guid}")]
    public async Task<IActionResult> DeleteAwardBudget(Guid id)
    {
        await _awardBudgetService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Employee Awards

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmployeeAwardSummaryDto>>> GetEmployeeAwards([FromQuery] Guid tenantId)
    {
        var result = await _employeeAwardService.GetAllAsync(tenantId);
        return Ok(result);
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<EmployeeAwardSummaryDto>>> GetEmployeeAwardsPaged(
        [FromQuery] Guid tenantId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? year = null,
        [FromQuery] AwardStatus? status = null)
    {
        var result = await _employeeAwardService.GetPagedAsync(tenantId, pageNumber, pageSize, searchTerm, year, status);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeAwardDto>> GetEmployeeAward(Guid id)
    {
        var result = await _employeeAwardService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<EmployeeAwardDetailDto>> GetEmployeeAwardDetails(Guid id)
    {
        var result = await _employeeAwardService.GetWithDetailsAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeAwardSummaryDto>>> GetAwardsByEmployee(Guid employeeId)
    {
        var result = await _employeeAwardService.GetByEmployeeIdAsync(employeeId);
        return Ok(result);
    }

    [HttpGet("pending/presentations")]
    public async Task<ActionResult<IEnumerable<EmployeeAwardSummaryDto>>> GetPendingPresentations([FromQuery] Guid tenantId)
    {
        var result = await _employeeAwardService.GetPendingPresentationsAsync(tenantId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeAwardDto>> CreateEmployeeAward(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid userId,
        [FromBody] CreateEmployeeAwardDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _employeeAwardService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetEmployeeAward), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeAwardDto>> UpdateEmployeeAward(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateEmployeeAwardDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _employeeAwardService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteEmployeeAward(Guid id)
    {
        await _employeeAwardService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/presentation")]
    public async Task<IActionResult> SchedulePresentation(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] ScheduleAwardPresentationDto dto)
    {
        dto.AwardId = id;
        await _employeeAwardService.SchedulePresentationAsync(userId, dto);
        return Ok(new { message = "Presentation scheduled" });
    }

    [HttpPost("{id:guid}/payment")]
    public async Task<IActionResult> ProcessAwardPayment(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] ProcessAwardPaymentDto dto)
    {
        dto.AwardId = id;
        await _employeeAwardService.ProcessPaymentAsync(userId, dto);
        return Ok(new { message = "Payment processed" });
    }

    #endregion

    #region Award Attachments

    [HttpGet("{awardId:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<AwardAttachmentDto>>> GetAwardAttachments(Guid awardId)
    {
        var result = await _awardAttachmentService.GetByAwardIdAsync(awardId);
        return Ok(result);
    }

    [HttpGet("attachments/{id:guid}")]
    public async Task<ActionResult<AwardAttachmentDto>> GetAwardAttachment(Guid id)
    {
        var result = await _awardAttachmentService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{awardId:guid}/attachments")]
    public async Task<ActionResult<AwardAttachmentDto>> AddAwardAttachment(
        Guid awardId,
        [FromQuery] Guid tenantId,
        [FromQuery] Guid userId,
        [FromBody] CreateAwardAttachmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.AwardId = awardId;
        var created = await _awardAttachmentService.CreateAsync(tenantId, awardId, userId, dto);
        return CreatedAtAction(nameof(GetAwardAttachment), new { id = created.Id }, created);
    }

    [HttpDelete("attachments/{id:guid}")]
    public async Task<IActionResult> DeleteAwardAttachment(Guid id)
    {
        await _awardAttachmentService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Nominations

    [HttpGet("nominations")]
    public async Task<ActionResult<IEnumerable<AwardNominationSummaryDto>>> GetNominations([FromQuery] Guid tenantId)
    {
        var result = await _nominationService.GetAllAsync(tenantId);
        return Ok(result);
    }

    [HttpGet("nominations/paged")]
    public async Task<ActionResult<PagedResult<AwardNominationSummaryDto>>> GetNominationsPaged(
        [FromQuery] Guid tenantId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? year = null,
        [FromQuery] AwardNominationStatus? status = null)
    {
        var result = await _nominationService.GetPagedAsync(tenantId, pageNumber, pageSize, searchTerm, year, status);
        return Ok(result);
    }

    [HttpGet("nominations/{id:guid}")]
    public async Task<ActionResult<AwardNominationDto>> GetNomination(Guid id)
    {
        var result = await _nominationService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("nominations/nominee/{nomineeId:guid}")]
    public async Task<ActionResult<IEnumerable<AwardNominationSummaryDto>>> GetNominationsByNominee(Guid nomineeId)
    {
        var result = await _nominationService.GetByNomineeIdAsync(nomineeId);
        return Ok(result);
    }

    [HttpPost("nominations")]
    public async Task<ActionResult<AwardNominationDto>> CreateNomination(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid nominatedById,
        [FromQuery] Guid userId,
        [FromBody] CreateAwardNominationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _nominationService.CreateAsync(tenantId, nominatedById, userId, dto);
        return CreatedAtAction(nameof(GetNomination), new { id = created.Id }, created);
    }

    [HttpPut("nominations/{id:guid}")]
    public async Task<ActionResult<AwardNominationDto>> UpdateNomination(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateAwardNominationDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _nominationService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [HttpDelete("nominations/{id:guid}")]
    public async Task<IActionResult> DeleteNomination(Guid id)
    {
        await _nominationService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("nominations/{id:guid}/submit")]
    public async Task<ActionResult<AwardNominationDto>> SubmitNomination(
        Guid id,
        [FromQuery] Guid userId)
    {
        var updated = await _nominationService.SubmitAsync(id, userId);
        return Ok(updated);
    }

    #endregion

    #region Team Award Nominees

    [HttpGet("nominations/{nominationId:guid}/team-nominees")]
    public async Task<ActionResult<IEnumerable<TeamAwardNomineeDto>>> GetTeamNominees(Guid nominationId)
    {
        var result = await _teamNomineeService.GetByNominationIdAsync(nominationId);
        return Ok(result);
    }

    [HttpGet("team-nominees/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TeamAwardNomineeDto>>> GetTeamNomineesByEmployee(Guid employeeId)
    {
        var result = await _teamNomineeService.GetByEmployeeIdAsync(employeeId);
        return Ok(result);
    }

    [HttpPost("nominations/{nominationId:guid}/team-nominees")]
    public async Task<ActionResult<TeamAwardNomineeDto>> AddTeamNominee(
        Guid nominationId,
        [FromQuery] Guid employeeId,
        [FromQuery] Guid userId,
        [FromBody] CreateTeamAwardNomineeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _teamNomineeService.AddAsync(nominationId, employeeId, userId, dto);
        return Ok(created);
    }

    [HttpPut("team-nominees/{id:guid}")]
    public async Task<IActionResult> UpdateTeamNominee(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateTeamAwardNomineeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _teamNomineeService.UpdateAsync(id, userId, dto);
        return Ok(new { message = "Team nominee updated" });
    }

    [HttpDelete("team-nominees/{id:guid}")]
    public async Task<IActionResult> RemoveTeamNominee(Guid id)
    {
        await _teamNomineeService.RemoveAsync(id);
        return NoContent();
    }

    #endregion

    #region Nominee Contributions

    [HttpGet("nominations/{nominationId:guid}/contributions")]
    public async Task<ActionResult<IEnumerable<AwardNomineeContributionDto>>> GetNomineeContributions(Guid nominationId)
    {
        var result = await _nomineeContributionService.GetByNominationIdAsync(nominationId);
        return Ok(result);
    }

    [HttpPost("nominations/{nominationId:guid}/contributions")]
    public async Task<ActionResult<AwardNomineeContributionDto>> AddNomineeContribution(
        Guid nominationId,
        [FromQuery] Guid userId,
        [FromBody] CreateAwardNomineeContributionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _nomineeContributionService.AddAsync(nominationId, userId, dto);
        return Ok(created);
    }

    [HttpPut("contributions/{id:guid}")]
    public async Task<IActionResult> UpdateNomineeContribution(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateAwardNomineeContributionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _nomineeContributionService.UpdateAsync(id, userId, dto);
        return Ok(new { message = "Contribution updated" });
    }

    [HttpDelete("contributions/{id:guid}")]
    public async Task<IActionResult> RemoveNomineeContribution(Guid id)
    {
        await _nomineeContributionService.RemoveAsync(id);
        return NoContent();
    }

    #endregion

    #region Nomination Attachments

    [HttpGet("nominations/{nominationId:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<AwardNominationAttachmentDto>>> GetNominationAttachments(Guid nominationId)
    {
        var result = await _nominationAttachmentService.GetByNominationIdAsync(nominationId);
        return Ok(result);
    }

    [HttpPost("nominations/{nominationId:guid}/attachments")]
    public async Task<ActionResult<AwardNominationAttachmentDto>> AddNominationAttachment(
        Guid nominationId,
        [FromQuery] Guid uploadedById,
        [FromQuery] Guid userId,
        [FromBody] CreateAwardNominationAttachmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _nominationAttachmentService.AddAsync(nominationId, uploadedById, userId, dto);
        return Ok(created);
    }

    [HttpPut("nomination-attachments/{id:guid}")]
    public async Task<IActionResult> UpdateNominationAttachment(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateAwardNominationAttachmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _nominationAttachmentService.UpdateAsync(id, userId, dto);
        return Ok(new { message = "Attachment updated" });
    }

    [HttpDelete("nomination-attachments/{id:guid}")]
    public async Task<IActionResult> RemoveNominationAttachment(Guid id)
    {
        await _nominationAttachmentService.RemoveAsync(id);
        return NoContent();
    }

    #endregion

    #region Award Committees

    [HttpGet("committees")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeDto>>> GetCommittees([FromQuery] Guid tenantId)
    {
        var result = await _committeeService.GetAllAsync(tenantId);
        return Ok(result);
    }

    [HttpGet("committees/active")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeDto>>> GetActiveCommittees([FromQuery] Guid tenantId)
    {
        var result = await _committeeService.GetActiveCommitteesAsync(tenantId);
        return Ok(result);
    }

    [HttpGet("committees/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeDto>> GetCommittee(Guid id)
    {
        var result = await _committeeService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("committees/{id:guid}/with-members")]
    public async Task<ActionResult<AwardCommitteeDto>> GetCommitteeWithMembers(Guid id)
    {
        var result = await _committeeService.GetWithMembersAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("committees/{id:guid}/has-quorum")]
    public async Task<ActionResult<bool>> CheckCommitteeQuorum(Guid id)
    {
        var result = await _committeeService.HasQuorumAsync(id);
        return Ok(new { committeeId = id, hasQuorum = result });
    }

    [HttpPost("committees")]
    public async Task<ActionResult<AwardCommitteeDto>> CreateCommittee(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid userId,
        [FromBody] CreateAwardCommitteeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _committeeService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetCommittee), new { id = created.Id }, created);
    }

    [HttpPut("committees/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeDto>> UpdateCommittee(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateAwardCommitteeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _committeeService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [HttpDelete("committees/{id:guid}")]
    public async Task<IActionResult> DeleteCommittee(Guid id)
    {
        await _committeeService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Award Committee Members

    [HttpGet("committees/{committeeId:guid}/members")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeMemberDto>>> GetCommitteeMembers(Guid committeeId)
    {
        var result = await _committeeMemberService.GetByCommitteeIdAsync(committeeId);
        return Ok(result);
    }

    [HttpGet("committees/{committeeId:guid}/members/active")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeMemberDto>>> GetActiveCommitteeMembers(Guid committeeId)
    {
        var result = await _committeeMemberService.GetActiveByCommitteeIdAsync(committeeId);
        return Ok(result);
    }

    [HttpGet("committee-members/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeMemberDto>> GetCommitteeMember(Guid id)
    {
        var result = await _committeeMemberService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("committee-members/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeMemberDto>>> GetMembershipsByEmployee(Guid employeeId)
    {
        var result = await _committeeMemberService.GetByEmployeeIdAsync(employeeId);
        return Ok(result);
    }

    [HttpGet("committees/{committeeId:guid}/members/{employeeId:guid}/is-active")]
    public async Task<ActionResult<bool>> CheckActiveMembership(Guid committeeId, Guid employeeId)
    {
        var result = await _committeeMemberService.IsActiveMemberAsync(committeeId, employeeId);
        return Ok(new { committeeId, employeeId, isActiveMember = result });
    }

    [HttpPost("committees/{committeeId:guid}/members")]
    public async Task<ActionResult<AwardCommitteeMemberDto>> AddCommitteeMember(
        Guid committeeId,
        [FromQuery] Guid userId,
        [FromBody] CreateAwardCommitteeMemberDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _committeeMemberService.AddAsync(committeeId, userId, dto);
        return CreatedAtAction(nameof(GetCommitteeMember), new { id = created.Id }, created);
    }

    [HttpPut("committee-members/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeMemberDto>> UpdateCommitteeMember(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateAwardCommitteeMemberDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _committeeMemberService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [HttpDelete("committee-members/{id:guid}")]
    public async Task<IActionResult> RemoveCommitteeMember(Guid id)
    {
        await _committeeMemberService.RemoveAsync(id);
        return NoContent();
    }

    [HttpPost("committee-members/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateCommitteeMember(
        Guid id,
        [FromQuery] Guid userId,
        [FromQuery] DateTime? endDate = null)
    {
        await _committeeMemberService.DeactivateAsync(id, userId, endDate);
        return Ok(new { message = "Committee member deactivated" });
    }

    #endregion

    #region Award Committee Reviews

    [HttpGet("nominations/{nominationId:guid}/reviews")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeReviewDto>>> GetNominationReviews(Guid nominationId)
    {
        var result = await _committeeReviewService.GetByNominationIdAsync(nominationId);
        return Ok(result);
    }

    [HttpGet("nominations/{nominationId:guid}/reviews/approval-count")]
    public async Task<ActionResult<int>> GetApprovalCount(Guid nominationId)
    {
        var result = await _committeeReviewService.GetApprovalCountAsync(nominationId);
        return Ok(new { nominationId, approvalCount = result });
    }

    [HttpGet("nominations/{nominationId:guid}/reviews/rejection-count")]
    public async Task<ActionResult<int>> GetRejectionCount(Guid nominationId)
    {
        var result = await _committeeReviewService.GetRejectionCountAsync(nominationId);
        return Ok(new { nominationId, rejectionCount = result });
    }

    [HttpGet("reviews/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeReviewDto>> GetReview(Guid id)
    {
        var result = await _committeeReviewService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("reviews/reviewer/{reviewerId:guid}")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeReviewDto>>> GetReviewsByReviewer(Guid reviewerId)
    {
        var result = await _committeeReviewService.GetByReviewerIdAsync(reviewerId);
        return Ok(result);
    }

    [HttpGet("reviews/reviewer/{reviewerId:guid}/pending")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeReviewDto>>> GetPendingReviews(Guid reviewerId)
    {
        var result = await _committeeReviewService.GetPendingReviewsAsync(reviewerId);
        return Ok(result);
    }

    [HttpPost("nominations/{nominationId:guid}/reviews")]
    public async Task<ActionResult<AwardCommitteeReviewDto>> SubmitReview(
        Guid nominationId,
        [FromQuery] Guid reviewerId,
        [FromQuery] Guid userId,
        [FromBody] SubmitCommitteeReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _committeeReviewService.SubmitReviewAsync(nominationId, reviewerId, userId, dto);
        return Ok(created);
    }

    [HttpPut("reviews/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeReviewDto>> UpdateReview(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateCommitteeReviewDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _committeeReviewService.UpdateReviewAsync(id, userId, dto);
        return Ok(updated);
    }

    #endregion

    #region Long Service Awards

    [HttpGet("long-service")]
    public async Task<ActionResult<IEnumerable<LongServiceAwardSummaryDto>>> GetLongServiceAwards([FromQuery] Guid tenantId)
    {
        var result = await _longServiceAwardService.GetAllAsync(tenantId);
        return Ok(result);
    }

    [HttpGet("long-service/paged")]
    public async Task<ActionResult<PagedResult<LongServiceAwardSummaryDto>>> GetLongServiceAwardsPaged(
        [FromQuery] Guid tenantId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? yearsOfService = null)
    {
        var result = await _longServiceAwardService.GetPagedAsync(tenantId, pageNumber, pageSize, searchTerm, yearsOfService);
        return Ok(result);
    }

    [HttpGet("long-service/{id:guid}")]
    public async Task<ActionResult<LongServiceAwardDto>> GetLongServiceAward(Guid id)
    {
        var result = await _longServiceAwardService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("long-service/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<LongServiceAwardSummaryDto>>> GetLongServiceByEmployee(Guid employeeId)
    {
        var result = await _longServiceAwardService.GetByEmployeeIdAsync(employeeId);
        return Ok(result);
    }

    [HttpGet("long-service/upcoming")]
    public async Task<ActionResult<IEnumerable<LongServiceAwardSummaryDto>>> GetUpcomingMilestones(
        [FromQuery] Guid tenantId,
        [FromQuery] int daysAhead = 90)
    {
        var result = await _longServiceAwardService.GetUpcomingMilestonesAsync(tenantId, daysAhead);
        return Ok(result);
    }

    [HttpGet("long-service/pending-processing")]
    public async Task<ActionResult<IEnumerable<LongServiceAwardSummaryDto>>> GetPendingProcessing([FromQuery] Guid tenantId)
    {
        var result = await _longServiceAwardService.GetPendingProcessingAsync(tenantId);
        return Ok(result);
    }

    [HttpPost("long-service")]
    public async Task<ActionResult<LongServiceAwardDto>> CreateLongServiceAward(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid userId,
        [FromBody] CreateLongServiceAwardDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _longServiceAwardService.CreateAsync(tenantId, userId, dto);
        return CreatedAtAction(nameof(GetLongServiceAward), new { id = created.Id }, created);
    }

    [HttpPut("long-service/{id:guid}")]
    public async Task<ActionResult<LongServiceAwardDto>> UpdateLongServiceAward(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] UpdateLongServiceAwardDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _longServiceAwardService.UpdateAsync(id, userId, dto);
        return Ok(updated);
    }

    [HttpDelete("long-service/{id:guid}")]
    public async Task<IActionResult> DeleteLongServiceAward(Guid id)
    {
        await _longServiceAwardService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("long-service/{id:guid}/process")]
    public async Task<IActionResult> ProcessLongServiceAward(
        Guid id,
        [FromQuery] Guid userId,
        [FromBody] ProcessLongServiceAwardDto dto)
    {
        dto.AwardId = id;
        await _longServiceAwardService.ProcessAsync(userId, dto);
        return Ok(new { message = "Long service award processed" });
    }

    #endregion
}

