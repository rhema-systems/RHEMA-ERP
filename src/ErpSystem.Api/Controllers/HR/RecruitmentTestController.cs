using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// HR's side of the recruitment test engine: authoring a paper, assigning it, and marking what
/// comes back (round 4, lane E).
/// </summary>
/// <remarks>
/// <para>Gated verb-mechanically on <c>HR.Recruitment.*</c> like the rest of the area — reads →
/// Read, desk operations → Write, deletes → Admin.</para>
///
/// <para>⚠ <b>Everything here shows the answers.</b> That is what makes it HR-only: the authoring
/// reads carry <c>IsCorrect</c> and <c>ExpectedAnswer</c>, and the marking reads carry both plus
/// what each candidate chose. The candidate's own surface is a different controller on a different
/// policy, served by DTOs that do not have those fields at all.</para>
/// </remarks>
[ApiController]
[Route("api/hr/recruitment/tests")]
[Authorize(Policy = "InternalOnly")]
[RecruitmentBusinessRules]
public class RecruitmentTestController : ControllerBase
{
    private readonly IRecruitmentTestService _service;

    public RecruitmentTestController(IRecruitmentTestService service)
    {
        _service = service;
    }

    // ── Papers ─────────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<RecruitmentTestDto>>> GetTests(
        [FromQuery] bool? activeOnly, CancellationToken ct)
        => Ok(await _service.GetTestsAsync(activeOnly, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<RecruitmentTestDto>> GetTest(Guid id, CancellationToken ct)
        => Ok(await _service.GetTestAsync(id, ct));

    /// <summary>The paper exactly as a candidate would be served it — no answers in the payload.</summary>
    [HttpGet("{id:guid}/preview")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<CandidateSittingDto>> Preview(Guid id, CancellationToken ct)
        => Ok(await _service.PreviewTestAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestDto>> CreateTest(
        [FromBody] CreateRecruitmentTestDto dto, CancellationToken ct)
        => Ok(await _service.CreateTestAsync(dto, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestDto>> UpdateTest(
        Guid id, [FromBody] UpdateRecruitmentTestDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("The route id and the payload id do not match.");
        return Ok(await _service.UpdateTestAsync(dto, ct));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestDto>> Activate(Guid id, CancellationToken ct)
        => Ok(await _service.SetTestActiveAsync(id, true, ct));

    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestDto>> Retire(Guid id, CancellationToken ct)
        => Ok(await _service.SetTestActiveAsync(id, false, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<ActionResult<bool>> DeleteTest(Guid id, CancellationToken ct)
        => Ok(await _service.DeleteTestAsync(id, ct));

    // ── Sections ───────────────────────────────────────────────────────────────

    [HttpPost("sections")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestSectionDto>> AddSection(
        [FromBody] CreateRecruitmentTestSectionDto dto, CancellationToken ct)
        => Ok(await _service.AddSectionAsync(dto, ct));

    [HttpPut("sections/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestSectionDto>> UpdateSection(
        Guid id, [FromBody] UpdateRecruitmentTestSectionDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("The route id and the payload id do not match.");
        return Ok(await _service.UpdateSectionAsync(dto, ct));
    }

    // Write, not Admin: deleting a section is part of authoring the same object, and the questions
    // under it survive — the same-object-authoring rule the area already follows.
    [HttpDelete("sections/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<bool>> DeleteSection(Guid id, CancellationToken ct)
        => Ok(await _service.DeleteSectionAsync(id, ct));

    // ── Questions ──────────────────────────────────────────────────────────────

    [HttpPost("questions")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestQuestionDto>> AddQuestion(
        [FromBody] CreateRecruitmentTestQuestionDto dto, CancellationToken ct)
        => Ok(await _service.AddQuestionAsync(dto, ct));

    [HttpPut("questions/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestQuestionDto>> UpdateQuestion(
        Guid id, [FromBody] UpdateRecruitmentTestQuestionDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("The route id and the payload id do not match.");
        return Ok(await _service.UpdateQuestionAsync(dto, ct));
    }

    [HttpDelete("questions/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<bool>> DeleteQuestion(Guid id, CancellationToken ct)
        => Ok(await _service.DeleteQuestionAsync(id, ct));

    // ── Assignment ─────────────────────────────────────────────────────────────

    [HttpGet("assignments")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<RecruitmentTestAssignmentDto>>> GetAssignments(
        [FromQuery] Guid? testId, [FromQuery] Guid? vacancyId, [FromQuery] Guid? applicationId,
        CancellationToken ct)
        => Ok(await _service.GetAssignmentsAsync(testId, vacancyId, applicationId, ct));

    [HttpPost("assignments")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestAssignmentDto>> Assign(
        [FromBody] CreateRecruitmentTestAssignmentDto dto, CancellationToken ct)
        => Ok(await _service.AssignAsync(dto, ct));

    [HttpDelete("assignments/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<bool>> DeleteAssignment(Guid id, CancellationToken ct)
        => Ok(await _service.DeleteAssignmentAsync(id, ct));

    /// <summary>Emails everybody this assignment reaches. Returns how many went out.</summary>
    [HttpPost("assignments/{id:guid}/invite")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<int>> Invite(Guid id, CancellationToken ct)
        => Ok(await _service.InviteAsync(id, ct));

    [HttpPost("assignments/{id:guid}/extra-attempt")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestAssignmentDto>> GrantExtraAttempt(
        Guid id, [FromBody] GrantExtraAttemptDto dto, CancellationToken ct)
    {
        if (id != dto.AssignmentId) return BadRequest("The route id and the payload id do not match.");
        return Ok(await _service.GrantExtraAttemptAsync(dto, ct));
    }

    // ── Sittings and marking ───────────────────────────────────────────────────

    [HttpGet("sittings")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<RecruitmentTestSittingDto>>> GetSittings(
        [FromQuery] Guid? testId, [FromQuery] Guid? assignmentId, [FromQuery] Guid? applicationId,
        [FromQuery] Guid? vacancyId, [FromQuery] RecruitmentSittingStatus? status,
        CancellationToken ct)
        => Ok(await _service.GetSittingsAsync(testId, assignmentId, applicationId, vacancyId, status, ct));

    [HttpGet("sittings/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<RecruitmentTestSittingDto>> GetSitting(Guid id, CancellationToken ct)
        => Ok(await _service.GetSittingAsync(id, ct));

    [HttpPost("sittings/answers/{answerId:guid}/mark")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestSittingDto>> MarkAnswer(
        Guid answerId, [FromBody] MarkFreeTextAnswerDto dto, CancellationToken ct)
    {
        if (answerId != dto.AnswerId) return BadRequest("The route id and the payload id do not match.");
        return Ok(await _service.MarkAnswerAsync(dto, ct));
    }

    /// <summary>
    /// Closes the marking: writes the result into the applicant's test ledger and re-scores the
    /// application, so the vacancy's test weighting is applied.
    /// </summary>
    [HttpPost("sittings/{id:guid}/finalise")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentTestSittingDto>> Finalise(
        Guid id, [FromBody] FinaliseSittingDto dto, CancellationToken ct)
    {
        if (id != dto.SittingId) return BadRequest("The route id and the payload id do not match.");
        return Ok(await _service.FinaliseSittingAsync(dto, ct));
    }
}
