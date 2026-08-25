using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Named interview templates: "which question types, how many of each". Applying one to an interview
/// scaffolds its question plans and draws the questions.
///
/// <para>HR-only for the same reason as <see cref="InterviewQuestionBankController"/> — a preset names
/// the shape of the interview, and it was previously readable and editable by any authenticated
/// employee.</para>
/// </summary>
[ApiController]
[Route("api/interview-question-presets")]
[Authorize(Policy = "InternalOnly")]
[RecruitmentBusinessRules]
public class InterviewQuestionPresetController : ControllerBase
{
    private readonly IInterviewQuestionPresetService _service;
    private readonly ICurrentUserService _currentUser;

    public InterviewQuestionPresetController(IInterviewQuestionPresetService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<InterviewQuestionPresetSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<InterviewQuestionPresetDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<InterviewQuestionPresetDto>> Create([FromBody] CreateInterviewQuestionPresetDto dto)
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
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<InterviewQuestionPresetDto>> Update(
        Guid id, [FromBody] UpdateInterviewQuestionPresetDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // ITEMS
    // =========================================================================

    [HttpPost("{presetId:guid}/items")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<InterviewQuestionPresetItemDto>> AddItem(
        Guid presetId, [FromBody] CreateInterviewQuestionPresetItemDto dto)
    {
        dto.PresetId = presetId;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddItemAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("{presetId:guid}/items/{itemId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<InterviewQuestionPresetItemDto>> UpdateItem(
        Guid presetId, Guid itemId, [FromBody] UpdateInterviewQuestionPresetItemDto dto)
    {
        if (itemId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateItemAsync(dto, employeeId.Value));
    }

    [HttpDelete("items/{itemId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteItem(Guid itemId)
    {
        await _service.DeleteItemAsync(itemId);
        return NoContent();
    }
}
