using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The interview question bank — question types and the questions inside them, with the weight and
/// score range each is marked against.
///
/// <para><b>HR-only, reads included.</b> The controller previously carried a bare <c>[Authorize]</c>, so
/// any authenticated employee could read the exact questions they would be asked at their next internal
/// interview, and edit the weights those answers are scored against. Panelists never need this
/// controller: the questions they ask come back embedded in their own interview's question plans.</para>
/// </summary>
[ApiController]
[Route("api/interview-question-bank")]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
[RecruitmentBusinessRules]
public class InterviewQuestionBankController : ControllerBase
{
    private readonly IJobInterviewQuestionBankService _service;
    private readonly ICurrentUserService _currentUser;

    public InterviewQuestionBankController(IJobInterviewQuestionBankService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUESTION TYPE QUERIES
    // =========================================================================

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<JobInterviewQuestionTypeSummaryDto>>> GetAllTypes()
        => Ok(await _service.GetAllQuestionTypesAsync());

    [HttpGet("types/{id:guid}")]
    public async Task<ActionResult<JobInterviewQuestionTypeDto>> GetTypeById(Guid id)
        => Ok(await _service.GetQuestionTypeByIdAsync(id));

    [HttpGet("types/code/{code}")]
    public async Task<ActionResult<JobInterviewQuestionTypeDto?>> GetTypeByCode(string code)
        => Ok(await _service.GetQuestionTypeByCodeAsync(code));

    [HttpGet("types/{id:guid}/with-questions")]
    public async Task<ActionResult<JobInterviewQuestionTypeDto?>> GetTypeWithQuestions(Guid id)
        => Ok(await _service.GetQuestionTypeWithQuestionsAsync(id));

    // =========================================================================
    // QUESTION TYPE CRUD
    // =========================================================================

    [HttpPost("types")]
    public async Task<ActionResult<JobInterviewQuestionTypeDto>> CreateType(
        [FromBody] CreateJobInterviewQuestionTypeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateQuestionTypeAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetTypeById), new { id = created.Id }, created);
    }

    [HttpPut("types/{id:guid}")]
    public async Task<ActionResult<JobInterviewQuestionTypeDto>> UpdateType(
        Guid id, [FromBody] UpdateJobInterviewQuestionTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateQuestionTypeAsync(dto, employeeId.Value));
    }

    [HttpDelete("types/{id:guid}")]
    public async Task<IActionResult> DeleteType(Guid id)
    {
        await _service.DeleteQuestionTypeAsync(id);
        return NoContent();
    }

    // =========================================================================
    // QUESTION DETAIL QUERIES
    // =========================================================================

    [HttpGet("questions")]
    public async Task<ActionResult<IEnumerable<JobInterviewQuestionDetailDto>>> GetAllQuestions()
        => Ok(await _service.GetAllQuestionDetailsAsync());

    [HttpGet("questions/active")]
    public async Task<ActionResult<IEnumerable<JobInterviewQuestionDetailDto>>> GetActiveQuestions(
        [FromQuery] Guid? questionTypeId = null)
        => Ok(await _service.GetActiveQuestionsAsync(questionTypeId));

    [HttpGet("questions/{id:guid}")]
    public async Task<ActionResult<JobInterviewQuestionDetailDto>> GetQuestionById(Guid id)
        => Ok(await _service.GetQuestionDetailByIdAsync(id));

    [HttpGet("types/{questionTypeId:guid}/questions")]
    public async Task<ActionResult<IEnumerable<JobInterviewQuestionDetailDto>>> GetQuestionsByType(Guid questionTypeId)
        => Ok(await _service.GetQuestionDetailsByTypeAsync(questionTypeId));

    // =========================================================================
    // QUESTION DETAIL CRUD
    // =========================================================================

    [HttpPost("questions")]
    public async Task<ActionResult<JobInterviewQuestionDetailDto>> CreateQuestion(
        [FromBody] CreateJobInterviewQuestionDetailDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateQuestionDetailAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetQuestionById), new { id = created.Id }, created);
    }

    [HttpPut("questions/{id:guid}")]
    public async Task<ActionResult<JobInterviewQuestionDetailDto>> UpdateQuestion(
        Guid id, [FromBody] UpdateJobInterviewQuestionDetailDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateQuestionDetailAsync(dto, employeeId.Value));
    }

    [HttpDelete("questions/{id:guid}")]
    public async Task<IActionResult> DeleteQuestion(Guid id)
    {
        await _service.DeleteQuestionDetailAsync(id);
        return NoContent();
    }
}
