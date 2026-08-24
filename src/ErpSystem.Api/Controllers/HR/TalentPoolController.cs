using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/talent-pool")]
[Authorize(Policy = "InternalOnly")]
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
    public async Task<ActionResult<TalentPoolPagedResultDto>> GetFiltered([FromQuery] TalentPoolFilterDto filter)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        // Inject tenant context into filter via a thin wrapper approach
        var result = await _candidateService.GetTalentPoolFilteredAsync(filter);
        return Ok(result);
    }

    [HttpGet("candidates/{id:guid}")]
    public async Task<ActionResult<TalentPoolCandidateDto>> GetCandidate(Guid id)
        => Ok(await _candidateService.GetTalentPoolCandidateAsync(id));

    [HttpPost("candidates/{id:guid}/add")]
    public async Task<ActionResult<bool>> AddToPool(Guid id, [FromBody] AddToTalentPoolDto dto)
    {
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null || employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _candidateService.AddToTalentPoolRichAsync(id, dto, employeeId.Value));
    }

    [HttpPost("candidates/{id:guid}/remove")]
    public async Task<ActionResult<bool>> RemoveFromPool(Guid id, [FromBody] RemoveFromTalentPoolDto dto)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _candidateService.RemoveFromTalentPoolRichAsync(id, dto, employeeId.Value));
    }

    [HttpPatch("candidates/{id:guid}/status")]
    public async Task<ActionResult<bool>> UpdateStatus(Guid id, [FromBody] TalentPoolCandidateStatus status)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _candidateService.UpdateTalentPoolStatusAsync(id, status, employeeId.Value));
    }

    [HttpPatch("candidates/{id:guid}/review-date")]
    public async Task<ActionResult<bool>> UpdateReviewDate(Guid id, [FromBody] DateTime reviewDate)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _candidateService.UpdateTalentPoolReviewDateAsync(id, reviewDate, employeeId.Value));
    }

    // =========================================================================
    // ANALYTICS
    // =========================================================================

    [HttpGet("analytics")]
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
    public async Task<ActionResult<List<TalentPoolVacancyMatchResultDto>>> MatchToVacancy(
        Guid vacancyId,
        [FromQuery] int topN = 20)
        => Ok(await _candidateService.MatchToVacancyAsync(vacancyId, topN));

    [HttpGet("candidates/{candidateId:guid}/match-vacancies")]
    public async Task<ActionResult<List<CandidateVacancyMatchResultDto>>> MatchCandidateToVacancies(
        Guid candidateId,
        [FromQuery] int topN = 10)
        => Ok(await _candidateService.MatchCandidateToVacanciesAsync(candidateId, topN));

    // =========================================================================
    // SEGMENTS
    // =========================================================================

    [HttpGet("segments")]
    public async Task<ActionResult<IEnumerable<CandidateTalentSegmentDto>>> GetSegments()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _segmentService.GetActiveAsync(tenantId.Value));
    }

    [HttpGet("segments/all")]
    public async Task<ActionResult<IEnumerable<CandidateTalentSegmentDto>>> GetAllSegments()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _segmentService.GetAllAsync(tenantId.Value));
    }

    [HttpGet("segments/{id:guid}")]
    public async Task<ActionResult<CandidateTalentSegmentDto>> GetSegment(Guid id)
        => Ok(await _segmentService.GetByIdAsync(id));

    [HttpPost("segments")]
    public async Task<ActionResult<CandidateTalentSegmentDto>> CreateSegment([FromBody] CreateCandidateTalentSegmentDto dto)
    {
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null || employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _segmentService.CreateAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("segments/{id:guid}")]
    public async Task<ActionResult<CandidateTalentSegmentDto>> UpdateSegment(Guid id, [FromBody] UpdateCandidateTalentSegmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _segmentService.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("segments/{id:guid}")]
    public async Task<ActionResult<bool>> DeleteSegment(Guid id)
        => Ok(await _segmentService.DeleteAsync(id));

    [HttpPost("candidates/{candidateId:guid}/segments")]
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

    [HttpDelete("candidates/{candidateId:guid}/segments/{segmentId:guid}")]
    public async Task<ActionResult<bool>> RemoveFromSegment(Guid candidateId, Guid segmentId)
        => Ok(await _segmentService.RemoveCandidateAsync(candidateId, segmentId));

    [HttpGet("candidates/{candidateId:guid}/segments")]
    public async Task<ActionResult<IEnumerable<CandidateSegmentMembershipDto>>> GetCandidateSegments(Guid candidateId)
        => Ok(await _segmentService.GetCandidateSegmentsAsync(candidateId));

    // =========================================================================
    // ENGAGEMENT EVENTS
    // =========================================================================

    [HttpGet("candidates/{candidateId:guid}/events")]
    public async Task<ActionResult<IEnumerable<CandidateEngagementEventDto>>> GetEvents(Guid candidateId)
        => Ok(await _engagementService.GetByCandidateIdAsync(candidateId));

    [HttpPost("candidates/{candidateId:guid}/events")]
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
    public async Task<ActionResult<bool>> DeleteEvent(Guid id)
        => Ok(await _engagementService.DeleteAsync(id));
}
