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
/// Job applications — a candidate against a vacancy, and everything the recruiter does to it.
///
/// <para><b>Reads are HR's, unlike <see cref="JobVacancyController"/>.</b> A vacancy is an advert and
/// the whole tenant may read it; an application carries the candidate's name, email, phone, date of
/// birth, CV, test results and rejection reasons, plus the EEO demographic report and the shortlist
/// CSV export. The controller previously carried a bare <c>[Authorize]</c>, so any authenticated
/// employee could read all of that — and shortlist, reject, delete or approve on top of it.</para>
///
/// <para>The exception is the internal job board at the bottom of this file: those four endpoints are
/// employee self-service and act only on the caller's own applications, so they stay open to any
/// authenticated employee. Note that ASP.NET Core <i>combines</i> authorization attributes rather than
/// letting the method override the class, which is why the gate is applied per endpoint here instead
/// of once at class level with exemptions.</para>
///
/// <para>Shortlist approval deliberately stays off the generic workflow engine: it is a flag on the
/// vacancy with several writers (shortlisting, un-shortlisting and auto-shortlisting all invalidate an
/// in-flight decision), which is the <c>AppraisalStatus</c> shape. The one thing the engine would have
/// given it — the initiator cannot approve their own submission — is enforced in the service.</para>
/// </summary>
[ApiController]
[Route("api/job-applications")]
[Authorize]
[RecruitmentBusinessRules]
public class JobApplicationController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IJobApplicationService _service;
    private readonly ICurrentUserService _currentUser;

    public JobApplicationController(IJobApplicationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<PagedResult<JobApplicationSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? vacancyId = null)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, vacancyId));

    [HttpGet("all")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicationSummaryDto>>> GetAll(
        [FromQuery] Guid? vacancyId = null)
        => Ok(await _service.GetAllAsync(vacancyId));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<JobApplicationDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{applicationNumber}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<JobApplicationDto?>> GetByApplicationNumber(string applicationNumber)
        => Ok(await _service.GetByApplicationNumberAsync(applicationNumber));

    [HttpGet("{id:guid}/details")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<JobApplicationDetailDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithFullDetailsAsync(id));

    [HttpGet("vacancy/{vacancyId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicationSummaryDto>>> GetByVacancy(Guid vacancyId)
        => Ok(await _service.GetByVacancyIdAsync(vacancyId));

    [HttpGet("candidate/{candidateId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicationSummaryDto>>> GetByCandidate(Guid candidateId)
        => Ok(await _service.GetByCandidateIdAsync(candidateId));

    [HttpGet("status/{status}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicationSummaryDto>>> GetByStatus(
        ApplicationStatus status, [FromQuery] Guid? vacancyId = null)
        => Ok(await _service.GetByStatusAsync(status, vacancyId));

    [HttpGet("shortlisted")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicationSummaryDto>>> GetAllShortlisted(
        [FromQuery] Guid? vacancyId = null)
        => Ok(await _service.GetShortlistedAsync(vacancyId));

    [HttpGet("vacancy/{vacancyId:guid}/shortlisted")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicationSummaryDto>>> GetShortlisted(Guid vacancyId)
        => Ok(await _service.GetShortlistedAsync(vacancyId));

    [HttpGet("stage/{pipelineStageId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicationSummaryDto>>> GetByCurrentStage(Guid pipelineStageId)
        => Ok(await _service.GetByCurrentStageAsync(pipelineStageId));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<JobApplicationDto>> Create([FromBody] CreateJobApplicationDto dto)
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

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================
    // Every action below takes its subject from the route and overwrites whatever id the body carried.
    // These DTOs all declare ApplicationId, and it used to be the only thing the service read — so a
    // POST to /{A}/reject with {"applicationId": B} rejected B while the audit trail, the URL and the
    // client all said A. Two of the endpoints (unshortlist, waitlist) already did this; the rest did not.

    [HttpPost("{id:guid}/shortlist")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> Shortlist(Guid id, [FromBody] ShortlistApplicationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ApplicationId = id;
        await _service.ShortlistAsync(dto, employeeId.Value);
        return Ok(new { message = "Application shortlisted." });
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectApplicationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ApplicationId = id;
        await _service.RejectAsync(dto, employeeId.Value);
        return Ok(new { message = "Application rejected." });
    }

    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawApplicationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ApplicationId = id;
        await _service.WithdrawAsync(dto, employeeId.Value);
        return Ok(new { message = "Application withdrawn." });
    }

    /// <summary>
    /// Moves an application to a pipeline stage. Identical in effect to
    /// <c>POST api/applications/move-stage</c> — both now run the same guarded transition.
    /// </summary>
    [HttpPost("{id:guid}/move-to-stage")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> MoveToStage(Guid id, [FromBody] MoveApplicationToStageDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ApplicationId = id;
        await _service.MoveToStageAsync(dto, employeeId.Value);
        return Ok(new { message = "Application moved to stage." });
    }

    // =========================================================================
    // STAGE HISTORY
    // =========================================================================

    [HttpGet("{applicationId:guid}/stage-history")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicationStageHistoryDto>>> GetStageHistory(Guid applicationId)
        => Ok(await _service.GetStageHistoryAsync(applicationId));

    // =========================================================================
    // TEST RESULTS
    // =========================================================================

    [HttpGet("{applicationId:guid}/test-results")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicantTestResultDto>>> GetTestResults(Guid applicationId)
        => Ok(await _service.GetTestResultsAsync(applicationId));

    [HttpGet("vacancy/{vacancyId:guid}/test-results")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicantTestResultDto>>> GetTestResultsByVacancy(Guid vacancyId)
        => Ok(await _service.GetTestResultsByVacancyAsync(vacancyId));

    [HttpGet("{applicationId:guid}/test-results/type/{testType}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicantTestResultDto>>> GetTestResultsByType(
        Guid applicationId, JobApplicantTestType testType)
        => Ok(await _service.GetTestResultsByTypeAsync(applicationId, testType));

    [HttpPost("{applicationId:guid}/test-results")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<JobApplicantTestResultDto>> AddTestResult(
        Guid applicationId, [FromBody] CreateJobApplicantTestResultDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.JobApplicationId = applicationId;
        return Ok(await _service.AddTestResultAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("test-results/{testResultId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<JobApplicantTestResultDto>> UpdateTestResult(
        Guid testResultId, [FromBody] UpdateJobApplicantTestResultDto dto)
    {
        if (testResultId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateTestResultAsync(dto, employeeId.Value));
    }

    [HttpDelete("test-results/{testResultId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteTestResult(Guid testResultId)
    {
        await _service.DeleteTestResultAsync(testResultId);
        return NoContent();
    }

    // =========================================================================
    // COMMUNICATIONS
    // =========================================================================

    [HttpGet("{applicationId:guid}/communications")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<JobApplicantCommunicationDto>>> GetCommunications(Guid applicationId)
        => Ok(await _service.GetCommunicationsAsync(applicationId));

    [HttpPost("{applicationId:guid}/communications")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<JobApplicantCommunicationDto>> AddCommunication(
        Guid applicationId, [FromBody] CreateJobApplicantCommunicationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.JobApplicationId = applicationId;
        return Ok(await _service.AddCommunicationAsync(dto, tenantId.Value, employeeId.Value));
    }

    // =========================================================================
    // SHORTLISTING — INDIVIDUAL ACTIONS
    // =========================================================================

    [HttpPost("{id:guid}/unshortlist")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> Unshortlist(Guid id, [FromBody] UnshortlistApplicationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");
        dto.ApplicationId = id;
        await _service.UnshortlistAsync(dto, employeeId.Value);
        return Ok(new { message = "Application un-shortlisted." });
    }

    [HttpPost("{id:guid}/waitlist")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> Waitlist(Guid id, [FromBody] WaitlistApplicationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");
        dto.ApplicationId = id;
        await _service.WaitlistAsync(dto, employeeId.Value);
        return Ok(new { message = "Application waitlisted." });
    }

    // =========================================================================
    // SHORTLISTING — BULK / AUTO
    // =========================================================================
    // The vacancy comes from the route and is passed to the service, which drops any id in the body
    // that belongs to a different vacancy. Without that, "reject everyone I did not shortlist here"
    // would act on whatever ids the caller supplied, from any vacancy in the tenant.

    [HttpPost("vacancy/{vacancyId:guid}/bulk-shortlist")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<RecruitmentBulkOperationResultDto>> BulkShortlist(
        Guid vacancyId, [FromBody] BulkShortlistDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.BulkShortlistAsync(vacancyId, dto, employeeId.Value));
    }

    [HttpPost("vacancy/{vacancyId:guid}/bulk-reject")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<RecruitmentBulkOperationResultDto>> BulkReject(
        Guid vacancyId, [FromBody] BulkRejectDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.BulkRejectAsync(vacancyId, dto, employeeId.Value));
    }

    [HttpPost("vacancy/{vacancyId:guid}/auto-shortlist")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<RecruitmentBulkOperationResultDto>> AutoShortlist(
        Guid vacancyId, [FromBody] AutoShortlistByScoreDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");
        dto.VacancyId = vacancyId;
        return Ok(await _service.AutoShortlistByScoreAsync(dto, employeeId.Value));
    }

    [HttpPost("vacancy/{vacancyId:guid}/shortlist/send-notifications")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<RecruitmentBulkOperationResultDto>> SendShortlistNotifications(Guid vacancyId)
    {
        return Ok(await _service.SendShortlistNotificationsAsync(vacancyId));
    }

    [HttpPost("vacancy/{vacancyId:guid}/rejections/send-notifications")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<RecruitmentBulkOperationResultDto>> SendRejectionNotifications(Guid vacancyId)
    {
        return Ok(await _service.SendRejectionNotificationsAsync(vacancyId));
    }

    // =========================================================================
    // SHORTLISTING — DASHBOARD
    // =========================================================================

    [HttpGet("vacancy/{vacancyId:guid}/shortlist/summary")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<ShortlistSummaryDto>> GetShortlistSummary(Guid vacancyId)
        => Ok(await _service.GetShortlistSummaryAsync(vacancyId));

    [HttpGet("vacancy/{vacancyId:guid}/shortlist/comparison")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<CandidateComparisonDto>> GetCandidateComparison(
        Guid vacancyId, [FromQuery] List<Guid>? applicationIds)
        => Ok(await _service.GetCandidateComparisonAsync(vacancyId, applicationIds ?? new List<Guid>()));

    // =========================================================================
    // SHORTLISTING — APPROVAL
    // =========================================================================

    [HttpPost("vacancy/{vacancyId:guid}/shortlist/submit-approval")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> SubmitShortlistForApproval(
        Guid vacancyId, [FromBody] SubmitShortlistForApprovalDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");
        dto.VacancyId = vacancyId;
        await _service.SubmitShortlistForApprovalAsync(dto, employeeId.Value);
        return Ok(new { message = "Shortlist submitted for approval." });
    }

    [HttpPost("vacancy/{vacancyId:guid}/shortlist/review-approval")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> ReviewShortlistApproval(
        Guid vacancyId, [FromBody] ReviewShortlistApprovalDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");
        dto.VacancyId = vacancyId;
        await _service.ReviewShortlistApprovalAsync(dto, employeeId.Value);
        return Ok(new { message = $"Shortlist {(dto.Approved ? "approved" : "rejected")}." });
    }

    [HttpPost("vacancy/{vacancyId:guid}/shortlist/recall-approval")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> RecallShortlistApproval(Guid vacancyId)
    {
        await _service.RecallShortlistApprovalAsync(vacancyId);
        return Ok(new { message = "Shortlist approval submission recalled." });
    }

    // =========================================================================
    // SCORING
    // =========================================================================

    [HttpPost("{id:guid}/score")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<ApplicationAutoScoreDto>> EvaluateScore(Guid id)
        => Ok(await _service.EvaluateApplicationScoreAsync(id));

    [HttpPost("vacancy/{vacancyId:guid}/score-all")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<ApplicationAutoScoreDto>>> EvaluateAllScores(Guid vacancyId)
        => Ok(await _service.EvaluateAllScoresForVacancyAsync(vacancyId));

    // =========================================================================
    // ENTERPRISE: AUDIT TRAIL / DECISION LOG
    // =========================================================================

    [HttpGet("{id:guid}/decision-log")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<ShortlistDecisionLogDto>>> GetDecisionLog(Guid id, CancellationToken ct)
        => Ok(await _service.GetShortlistDecisionLogAsync(id, ct));

    // =========================================================================
    // ENTERPRISE: PANEL SCORING (MULTI-REVIEWER)
    // =========================================================================

    [HttpPost("{id:guid}/reviews")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<ShortlistReviewDto>> AddReview(
        Guid id, [FromBody] CreateShortlistReviewDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        dto.ApplicationId = id;
        return Ok(await _service.AddShortlistReviewAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/reviews/aggregated")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AggregatedReviewScoreDto>> GetAggregatedReviewScore(Guid id, CancellationToken ct)
        => Ok(await _service.GetAggregatedReviewScoreAsync(id, ct));

    /// <summary>
    /// Commits the caller's own review so it counts toward the aggregate. Refused for anyone else's
    /// review — finalizing is the reviewer signing off their judgement, not a clerical step.
    /// </summary>
    [HttpPost("reviews/{reviewId:guid}/finalize")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<ShortlistReviewDto>> FinalizeReview(Guid reviewId, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.FinalizeShortlistReviewAsync(reviewId, employeeId.Value, ct));
    }

    // =========================================================================
    // ENTERPRISE: EEO / DIVERSITY COMPLIANCE REPORT
    // =========================================================================

    [HttpGet("vacancy/{vacancyId:guid}/eeo-report")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<EeoComplianceReportDto>> GetEeoReport(Guid vacancyId, CancellationToken ct)
        => Ok(await _service.GetEeoReportAsync(vacancyId, ct));

    // =========================================================================
    // ENTERPRISE: SLA / TIME-TO-SHORTLIST TRACKING
    // =========================================================================

    [HttpGet("vacancy/{vacancyId:guid}/shortlist/sla")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<ShortlistSlaStatusDto>> GetShortlistSla(Guid vacancyId, CancellationToken ct)
        => Ok(await _service.GetShortlistSlaStatusAsync(vacancyId, ct));

    // =========================================================================
    // ENTERPRISE: BLIND SCREENING
    // =========================================================================

    [HttpGet("vacancy/{vacancyId:guid}/blind-applications")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<BlindApplicationSummaryDto>>> GetBlindApplications(
        Guid vacancyId, CancellationToken ct)
        => Ok(await _service.GetBlindApplicationsAsync(vacancyId, ct));

    // =========================================================================
    // ENTERPRISE: INTERNAL CANDIDATE PREFERENCING
    // =========================================================================

    [HttpPost("{id:guid}/mark-internal")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> MarkAsInternal(Guid id, [FromBody] MarkInternalCandidateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        dto.ApplicationId = id;
        await _service.MarkAsInternalCandidateAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Application marked as internal candidate." });
    }

    [HttpDelete("{id:guid}/mark-internal")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> UnmarkAsInternal(Guid id, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        await _service.UnmarkAsInternalCandidateAsync(id, employeeId.Value, ct);
        return Ok(new { message = "Internal candidate flag removed." });
    }

    // =========================================================================
    // ENTERPRISE: CSV EXPORT
    // =========================================================================

    [HttpGet("vacancy/{vacancyId:guid}/shortlist/export")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> ExportShortlistCsv(Guid vacancyId, CancellationToken ct)
    {
        var csv = await _service.GetShortlistCsvExportAsync(vacancyId, ct);
        return File(csv, "text/csv", $"shortlist-{vacancyId:N}.csv");
    }

    // =========================================================================
    // INTERNAL JOB BOARD — Employee self-service
    // =========================================================================
    // Deliberately NOT HR-gated: these four act only on the caller's own applications, taking the
    // employee from the token, and exist so an employee can apply for an internal vacancy.

    /// <summary>
    /// Allows the currently authenticated employee to apply for an internal vacancy
    /// via the Internal Job Board. Resolves/creates a shadow JobCandidate automatically.
    /// </summary>
    [HttpPost("apply-internal")]
    public async Task<ActionResult<JobApplicationDto>> ApplyInternal(
        [FromBody] InternalApplyForVacancyDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");

        var created = await _service.InternalApplyAsync(dto, employeeId.Value, tenantId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Returns all applications submitted by the currently authenticated employee
    /// through the Internal Job Board. Used to show "Already Applied" status.
    /// </summary>
    [HttpGet("my-applications")]
    public async Task<ActionResult<IEnumerable<JobApplicationSummaryDto>>> GetMyApplications(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record.");

        var results = await _service.GetByInternalEmployeeAsync(employeeId.Value, ct);
        return Ok(results);
    }

    /// <summary>Saves or updates a draft internal application (status = Draft).</summary>
    [HttpPost("internal/draft")]
    public async Task<ActionResult<JobApplicationDto>> SaveInternalDraft(
        [FromBody] InternalSaveDraftDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId   == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        var result = await _service.InternalSaveDraftAsync(dto, employeeId.Value, tenantId.Value, ct);
        return Ok(result);
    }

    /// <summary>Submits an existing internal draft application (Draft → Submitted).</summary>
    [HttpPut("internal/{id:guid}/submit")]
    public async Task<ActionResult<JobApplicationDto>> SubmitInternalDraft(
        Guid id,
        [FromBody] InternalSubmitDraftDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId   == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        var result = await _service.InternalSubmitDraftAsync(id, dto, employeeId.Value, tenantId.Value, ct);
        return Ok(result);
    }
}
