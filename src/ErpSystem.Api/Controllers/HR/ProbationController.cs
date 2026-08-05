using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>Request body for extending a probation period.</summary>
public sealed record ExtendProbationRequest(DateTime NewEndDate, string Reason);

[ApiController]
[Route("api/probations")]
[Authorize]
public class ProbationController : ControllerBase
{
    private readonly IProbationService _service;
    private readonly ICurrentUserService _currentUser;

    public ProbationController(IProbationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // PROBATION QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProbationPeriodDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<ProbationPeriodSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId));

    [HttpGet("{id:guid}/with-reviews")]
    public async Task<ActionResult<ProbationPeriodDetailDto>> GetWithReviews(Guid id)
        => Ok(await _service.GetWithReviewsAsync(id));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<ProbationPeriodSummaryDto>>> GetByStatus(ProbationStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ProbationPeriodSummaryDto>>> GetActive()
        => Ok(await _service.GetActiveProbationsAsync());

    [HttpGet("ending-within")]
    public async Task<ActionResult<IEnumerable<ProbationPeriodSummaryDto>>> GetEndingWithin(
        [FromQuery] int daysAhead = 30)
        => Ok(await _service.GetEndingWithinAsync(daysAhead));

    // =========================================================================
    // PROBATION CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<ProbationPeriodDto>> Create([FromBody] CreateProbationPeriodDto dto)
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
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // PROBATION WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/extend")]
    public async Task<IActionResult> Extend(Guid id, [FromBody] ExtendProbationRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ExtendAsync(id, request.NewEndDate, request.Reason, employeeId.Value);
        return Ok(new { message = "Probation extended." });
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ConfirmAsync(id, employeeId.Value);
        return Ok(new { message = "Probation confirmed." });
    }

    [HttpPost("{id:guid}/terminate")]
    public async Task<IActionResult> Terminate(Guid id, [FromBody] TerminateProbationPeriodDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.TerminateAsync(dto, employeeId.Value);
        return Ok(new { message = "Probation terminated." });
    }

    // =========================================================================
    // REVIEWS
    // =========================================================================

    [HttpGet("{probationId:guid}/reviews")]
    public async Task<ActionResult<IEnumerable<ProbationReviewDto>>> GetReviews(Guid probationId)
        => Ok(await _service.GetReviewsAsync(probationId));

    [HttpGet("reviews/status/{status}")]
    public async Task<ActionResult<IEnumerable<ProbationReviewDto>>> GetReviewsByStatus(ProbationReviewStatus status)
        => Ok(await _service.GetReviewsByStatusAsync(status));

    [HttpGet("reviews/overdue")]
    public async Task<ActionResult<IEnumerable<ProbationReviewDto>>> GetOverdueReviews()
        => Ok(await _service.GetOverdueReviewsAsync());

    [HttpGet("reviews/reviewer/{reviewerEmployeeId:guid}")]
    public async Task<ActionResult<IEnumerable<ProbationReviewDto>>> GetReviewsByReviewer(Guid reviewerEmployeeId)
        => Ok(await _service.GetReviewsByReviewerAsync(reviewerEmployeeId));

    [HttpPost("{probationId:guid}/reviews")]
    public async Task<ActionResult<ProbationReviewDto>> AddReview(
        Guid probationId, [FromBody] CreateProbationReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddReviewAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("reviews/{reviewId:guid}")]
    public async Task<ActionResult<ProbationReviewDto>> UpdateReview(
        Guid reviewId, [FromBody] UpdateProbationReviewDto dto)
    {
        if (reviewId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateReviewAsync(dto, employeeId.Value));
    }

    [HttpPost("reviews/{reviewId:guid}/complete")]
    public async Task<IActionResult> CompleteReview(Guid reviewId)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CompleteReviewAsync(reviewId, employeeId.Value);
        return Ok(new { message = "Review completed." });
    }
}
