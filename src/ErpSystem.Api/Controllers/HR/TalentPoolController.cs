using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Recruitment's candidate talent pool (CRM): pooled candidates, talent segments, engagement
/// events, analytics and vacancy matching. ⚠ Not the succession module's employee talent pools —
/// those are <c>api/talent-pools</c> on <c>TalentPoolsController</c>.
/// </summary>
/// <remarks>
/// <b>W3 slice 14, closing a slice-9 census gap.</b> The recruitment conversion swept the 15
/// controllers its census found; this one — bare since the port, no screen calling it — was not
/// among them, so any internal user could read pooled candidates' profiles and engagement
/// history and run desk writes. Gated verb-mechanically on <c>HR.Recruitment.*</c> like the rest
/// of the area: reads → Read, desk ops → Write, deletes → Admin (segment unassign stays Write —
/// the same-object-authoring rule).
/// </remarks>
[ApiController]
[Route("api/talent-pool")]
[Authorize(Policy = "InternalOnly")]
[RecruitmentBusinessRules] // without it the services' ArgumentException "not found" answered as a generic 400/500 instead of 404
public class TalentPoolController : ControllerBase
{
    private readonly IJobCandidateService _candidateService;
    private readonly ICandidateTalentSegmentService _segmentService;
    private readonly ICandidateEngagementEventService _engagementService;
    private readonly ICurrentUserService _currentUser;

    public TalentPoolController(
        IJobCandidateService candidateService,
        ICandidateTalentSegmentService segmentService,
        ICandidateEngagementEventService engagementService,
        ICurrentUserService currentUser)
    {
        _candidateService  = candidateService;
        _segmentService    = segmentService;
        _engagementService = engagementService;
        _currentUser       = currentUser;
    }

    // =========================================================================
    // POOL CANDIDATES
    // =========================================================================

    [HttpGet("candidates")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<TalentPoolPagedResultDto>> GetFiltered([FromQuery] TalentPoolFilterDto filter)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _candidateService.GetTalentPoolFilteredAsync(filter));
    }

    [HttpGet("candidates/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<TalentPoolCandidateDto>> GetCandidate(Guid id)
        => Ok(await _candidateService.GetTalentPoolCandidateAsync(id));

    [HttpPost("candidates/{id:guid}/add")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<bool>> AddToPool(Guid id, [FromBody] AddToTalentPoolDto dto)
    {
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null || employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _candidateService.AddToTalentPoolRichAsync(id, dto, employeeId.Value));
    }

    [HttpPost("candidates/{id:guid}/remove")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<bool>> RemoveFromPool(Guid id, [FromBody] RemoveFromTalentPoolDto dto)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _candidateService.RemoveFromTalentPoolRichAsync(id, dto, employeeId.Value));
    }

    [HttpPatch("candidates/{id:guid}/status")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<bool>> UpdateStatus(Guid id, [FromBody] UpdateTalentPoolStatusDto dto)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _candidateService.UpdateTalentPoolStatusAsync(id, dto.Status, employeeId.Value));
    }

    [HttpPatch("candidates/{id:guid}/review-date")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<bool>> UpdateReviewDate(Guid id, [FromBody] UpdateTalentPoolReviewDateDto dto)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _candidateService.UpdateTalentPoolReviewDateAsync(id, dto.ReviewDate, employeeId.Value));
    }

    // =========================================================================
    // ANALYTICS
    // =========================================================================

    [HttpGet("analytics")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<TalentPoolAnalyticsDto>> GetAnalytics()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _candidateService.GetTalentPoolAnalyticsAsync(tenantId.Value));
    }

    // =========================================================================
    // BULK OPERATIONS
    // =========================================================================

    [HttpPost("bulk")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<RecruitmentBulkOperationResultDto>> BulkOperation([FromBody] BulkTalentPoolOperationDto dto)
    {
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null || employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _candidateService.BulkTalentPoolOperationAsync(dto, tenantId.Value, employeeId.Value));
    }

    // =========================================================================
    // VACANCY MATCHING
    // =========================================================================

    [HttpGet("match/{vacancyId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<List<TalentPoolVacancyMatchResultDto>>> MatchToVacancy(
        Guid vacancyId,
        [FromQuery] int topN = 20)
        => Ok(await _candidateService.MatchToVacancyAsync(vacancyId, topN));

    [HttpGet("candidates/{candidateId:guid}/match-vacancies")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<List<CandidateVacancyMatchResultDto>>> MatchCandidateToVacancies(
        Guid candidateId,
        [FromQuery] int topN = 10)
        => Ok(await _candidateService.MatchCandidateToVacanciesAsync(candidateId, topN));

    // =========================================================================
    // SEGMENTS
    // =========================================================================

    [HttpGet("segments")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<CandidateTalentSegmentDto>>> GetSegments()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _segmentService.GetActiveAsync(tenantId.Value));
    }

    [HttpGet("segments/all")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<CandidateTalentSegmentDto>>> GetAllSegments()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _segmentService.GetAllAsync(tenantId.Value));
    }

    [HttpGet("segments/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<CandidateTalentSegmentDto>> GetSegment(Guid id)
        => Ok(await _segmentService.GetByIdAsync(id));

    [HttpPost("segments")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<CandidateTalentSegmentDto>> CreateSegment([FromBody] CreateCandidateTalentSegmentDto dto)
    {
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null || employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _segmentService.CreateAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("segments/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<CandidateTalentSegmentDto>> UpdateSegment(Guid id, [FromBody] UpdateCandidateTalentSegmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _segmentService.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("segments/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<ActionResult<bool>> DeleteSegment(Guid id)
        => Ok(await _segmentService.DeleteAsync(id));

    [HttpPost("candidates/{candidateId:guid}/segments")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<CandidateSegmentMembershipDto>> AddToSegment(
        Guid candidateId,
        [FromBody] AddCandidateToSegmentDto dto)
    {
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null || employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _segmentService.AddCandidateAsync(candidateId, dto, tenantId.Value, employeeId.Value));
    }

    // Unassigning a candidate from a segment is the same desk act as assigning them — the
    // slice-10 unassign precedent — not data destruction; deleting the segment itself is Admin.
    [HttpDelete("candidates/{candidateId:guid}/segments/{segmentId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<bool>> RemoveFromSegment(Guid candidateId, Guid segmentId)
        => Ok(await _segmentService.RemoveCandidateAsync(candidateId, segmentId));

    [HttpGet("candidates/{candidateId:guid}/segments")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<CandidateSegmentMembershipDto>>> GetCandidateSegments(Guid candidateId)
        => Ok(await _segmentService.GetCandidateSegmentsAsync(candidateId));

    // =========================================================================
    // ENGAGEMENT EVENTS
    // =========================================================================

    [HttpGet("candidates/{candidateId:guid}/events")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<CandidateEngagementEventDto>>> GetEvents(Guid candidateId)
        => Ok(await _engagementService.GetByCandidateIdAsync(candidateId));

    [HttpPost("candidates/{candidateId:guid}/events")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<CandidateEngagementEventDto>> LogEvent(
        Guid candidateId,
        [FromBody] CreateCandidateEngagementEventDto dto)
    {
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null || employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        dto.JobCandidateId = candidateId;
        return Ok(await _engagementService.LogEventAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpDelete("events/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<ActionResult<bool>> DeleteEvent(Guid id)
        => Ok(await _engagementService.DeleteAsync(id));
}
