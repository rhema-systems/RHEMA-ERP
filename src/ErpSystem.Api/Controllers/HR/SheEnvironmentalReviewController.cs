using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Environmental compliance reviews, screening and clearance (FR-ENV-001–016)
/// plus the monthly environmental report (FR-ENV-033–034). The Project module's
/// automatic triggering and the procurement/construction hard block
/// (FR-ENV-008/010/012/013, decision DR-09) are a stubbed seam — reviews are
/// created manually against a free-text project reference until a Project
/// trigger source exists. HR-gated end to end.
/// </summary>
[ApiController]
[Route("api/safety/environmental")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SheEnvironmentalReviewController : SheApiControllerBase
{
    private readonly ISheEnvironmentalReviewService _reviews;
    private readonly ISheMonthlyEnvironmentalReportService _reports;

    public SheEnvironmentalReviewController(
        ISheEnvironmentalReviewService reviews,
        ISheMonthlyEnvironmentalReportService reports,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _reviews = reviews;
        _reports = reports;
    }

    // ── Compliance reviews (FR-ENV-001–016) ──
    [HttpGet("reviews")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalReviewSummaryDto>>> GetReviews(
        [FromQuery] SheEnvironmentalReviewStatus? status,
        [FromQuery] string? search)
        => Ok(await _reviews.GetAllAsync(status, search));

    [HttpGet("reviews/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheEnvironmentalReviewDto>> GetReview(Guid id)
        => Ok(await _reviews.GetByIdAsync(id));

    [HttpPost("reviews")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalReviewDto>> CreateReview([FromBody] CreateSheEnvironmentalReviewDto dto)
    {
        var created = await _reviews.CreateAsync(dto, TenantId, UserId, UserId);
        return CreatedAtAction(nameof(GetReview), new { id = created.Id }, created);
    }

    /// <summary>Pre-decision edits only — refused (422) once the review is decided.</summary>
    [HttpPut("reviews/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalReviewDto>> UpdateReview(Guid id, [FromBody] UpdateSheEnvironmentalReviewDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _reviews.UpdateAsync(dto, UserId));
    }

    /// <summary>Undecided reviews only — a decided review is compliance archive.</summary>
    [HttpDelete("reviews/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        await _reviews.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>The FR-ENV-009 screening determination — what this work requires before it may proceed.</summary>
    [HttpPost("reviews/{id:guid}/screening")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalReviewDto>> RecordScreening(Guid id, [FromBody] SheEnvironmentalScreeningDto dto)
        => Ok(await _reviews.RecordScreeningAsync(id, dto, UserId));

    [HttpPost("reviews/{id:guid}/request-corrections")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalReviewDto>> RequestCorrections(Guid id, [FromBody] RequestSheReviewCorrectionsDto dto)
        => Ok(await _reviews.RequestCorrectionsAsync(id, dto, UserId));

    /// <summary>Refused (422) until the screening determination has been recorded.</summary>
    [HttpPost("reviews/{id:guid}/approve")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalReviewDto>> ApproveReview(Guid id, [FromBody] SheEnvironmentalReviewDecisionDto dto)
        => Ok(await _reviews.ApproveAsync(id, dto, UserId));

    [HttpPost("reviews/{id:guid}/reject")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalReviewDto>> RejectReview(Guid id, [FromBody] SheEnvironmentalReviewDecisionDto dto)
        => Ok(await _reviews.RejectAsync(id, dto, UserId));

    /// <summary>FR-ENV-005 — only where the screening routed the review to management.</summary>
    [HttpPost("reviews/{id:guid}/management-approve")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalReviewDto>> ManagementApprove(Guid id, [FromBody] SheEnvironmentalReviewDecisionDto dto)
        => Ok(await _reviews.ManagementApproveAsync(id, dto, UserId));

    /// <summary>FR-ENV-011 — records the EPA submission on an approved review.</summary>
    [HttpPost("reviews/{id:guid}/record-epa-submission")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalReviewDto>> RecordEpaSubmission(Guid id, [FromBody] RecordSheEpaSubmissionDto dto)
        => Ok(await _reviews.RecordEpaSubmissionAsync(id, dto, UserId));

    /// <summary>
    /// FR-ENV-016 — refused (422) until approval, management approval (where
    /// required) and the EPA submission (where required) are all in place.
    /// </summary>
    [HttpPost("reviews/{id:guid}/issue-clearance")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalReviewDto>> IssueClearance(Guid id, [FromBody] SheEnvironmentalReviewDecisionDto dto)
        => Ok(await _reviews.IssueClearanceAsync(id, dto, UserId));

    [HttpPost("reviews/{id:guid}/approve-commencement")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalReviewDto>> ApproveCommencement(Guid id, [FromBody] SheEnvironmentalReviewDecisionDto dto)
        => Ok(await _reviews.ApproveCommencementAsync(id, dto, UserId));

    /// <summary>The FR-ENV-016 Environmental Clearance Report, assembled from the review and its trail.</summary>
    [HttpGet("reviews/{id:guid}/clearance-report")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheEnvironmentalClearanceReportDto>> GetClearanceReport(Guid id)
        => Ok(await _reviews.GetClearanceReportAsync(id));

    // ── Monthly environmental reports (FR-ENV-033–034) ──
    [HttpGet("monthly-reports")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheMonthlyEnvironmentalReportSummaryDto>>> GetMonthlyReports([FromQuery] int? year)
        => Ok(await _reports.GetAllAsync(year));

    [HttpGet("monthly-reports/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheMonthlyEnvironmentalReportDto>> GetMonthlyReport(Guid id)
        => Ok(await _reports.GetByIdAsync(id));

    /// <summary>Generates (or regenerates) a period's report. Refused (422) once the report is submitted.</summary>
    [HttpPost("monthly-reports/generate")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheMonthlyEnvironmentalReportDto>> GenerateMonthlyReport([FromBody] GenerateSheMonthlyReportDto dto)
        => Ok(await _reports.GenerateAsync(dto, TenantId, UserId));

    /// <summary>Edits the officer narrative — refused (422) once the report is submitted.</summary>
    [HttpPut("monthly-reports/{id:guid}/summary")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheMonthlyEnvironmentalReportDto>> UpdateMonthlyReportSummary(Guid id, [FromBody] UpdateSheMonthlyReportSummaryDto dto)
        => Ok(await _reports.UpdateOfficerSummaryAsync(id, dto, UserId));

    /// <summary>FR-ENV-034 — submits to management and freezes the report as retained history.</summary>
    [HttpPost("monthly-reports/{id:guid}/submit")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheMonthlyEnvironmentalReportDto>> SubmitMonthlyReport(Guid id)
        => Ok(await _reports.SubmitAsync(id, UserId));
}
