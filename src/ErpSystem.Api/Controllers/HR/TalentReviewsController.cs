using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/talent-reviews")]
[Authorize]
public class TalentReviewsController : ControllerBase
{
    private readonly ITalentReviewSessionService _service;
    private readonly ICurrentUserService _currentUser;

    public TalentReviewsController(ITalentReviewSessionService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // SESSION QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<PagedResult<TalentReviewSessionSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<TalentReviewSessionSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TalentReviewSessionDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("{id:guid}/with-ratings")]
    public async Task<ActionResult<TalentReviewSessionDto>> GetWithRatings(Guid id)
        => Ok(await _service.GetWithRatingsAsync(id));

    [HttpGet("year/{reviewYear:int}")]
    public async Task<ActionResult<IEnumerable<TalentReviewSessionSummaryDto>>> GetByYear(int reviewYear)
        => Ok(await _service.GetByYearAsync(reviewYear));

    [HttpGet("unit/{organizationUnitId:guid}")]
    public async Task<ActionResult<IEnumerable<TalentReviewSessionSummaryDto>>> GetByOrganizationUnit(Guid organizationUnitId)
        => Ok(await _service.GetByOrganizationUnitAsync(organizationUnitId));

    [HttpGet("finalized")]
    public async Task<ActionResult<IEnumerable<TalentReviewSessionSummaryDto>>> GetFinalized()
        => Ok(await _service.GetFinalizedSessionsAsync());

    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<TalentReviewSessionSummaryDto>>> GetPending()
        => Ok(await _service.GetPendingSessionsAsync());

    // =========================================================================
    // SESSION CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<TalentReviewSessionDto>> Create([FromBody] CreateTalentReviewSessionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TalentReviewSessionDto>> Update(Guid id, [FromBody] UpdateTalentReviewSessionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpPost("{id:guid}/finalize")]
    public async Task<IActionResult> Finalize(Guid id, [FromBody] FinalizeTalentReviewSessionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.SessionId = id;
        await _service.FinalizeAsync(dto);
        return Ok(new { message = "Talent review session finalized." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // RATING QUERIES
    // =========================================================================

    [HttpGet("{id:guid}/ratings")]
    public async Task<ActionResult<IEnumerable<TalentReviewRatingSummaryDto>>> GetRatings(Guid id)
        => Ok(await _service.GetRatingsForSessionAsync(id));

    [HttpGet("{id:guid}/ratings/calibrated")]
    public async Task<ActionResult<IEnumerable<TalentReviewRatingSummaryDto>>> GetCalibratedRatings(Guid id)
        => Ok(await _service.GetCalibratedRatingsAsync(id));

    [HttpGet("{id:guid}/ratings/pending-calibration")]
    public async Task<ActionResult<IEnumerable<TalentReviewRatingSummaryDto>>> GetPendingCalibration(Guid id)
        => Ok(await _service.GetPendingCalibrationAsync(id));

    [HttpGet("{id:guid}/ratings/nine-box")]
    public async Task<ActionResult<IEnumerable<TalentReviewRatingSummaryDto>>> GetByNineBoxPosition(
        Guid id,
        [FromQuery] PerformanceRating performance,
        [FromQuery] PotentialRating potential)
        => Ok(await _service.GetByNineBoxPositionAsync(id, performance, potential));

    [HttpGet("ratings/{ratingId:guid}")]
    public async Task<ActionResult<TalentReviewRatingDto?>> GetRatingById(Guid ratingId)
        => Ok(await _service.GetRatingByIdAsync(ratingId));

    [HttpGet("ratings/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TalentReviewRatingSummaryDto>>> GetRatingsForEmployee(Guid employeeId)
        => Ok(await _service.GetRatingsForEmployeeAsync(employeeId));

    [HttpGet("ratings/employee/{employeeId:guid}/latest-confirmed")]
    public async Task<ActionResult<TalentReviewRatingDto?>> GetLatestConfirmedRating(Guid employeeId)
        => Ok(await _service.GetLatestConfirmedRatingForEmployeeAsync(employeeId));

    // =========================================================================
    // RATING CRUD & WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/ratings")]
    public async Task<ActionResult<TalentReviewRatingDto>> AddRating(Guid id, [FromBody] CreateTalentReviewRatingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.SessionId = id;
        var created = await _service.AddRatingAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetRatings), new { id }, created);
    }

    [HttpPut("ratings/{ratingId:guid}")]
    public async Task<ActionResult<TalentReviewRatingDto>> UpdateRating(Guid ratingId, [FromBody] UpdateTalentReviewRatingDto dto)
    {
        if (ratingId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateRatingAsync(dto, employeeId.Value));
    }

    [HttpPost("ratings/{ratingId:guid}/confirm-calibration")]
    public async Task<IActionResult> ConfirmCalibration(Guid ratingId, [FromBody] ConfirmCalibrationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.RatingId = ratingId;
        await _service.ConfirmCalibrationAsync(dto);
        return Ok(new { message = "Calibration confirmed." });
    }

    [HttpDelete("ratings/{ratingId:guid}")]
    public async Task<IActionResult> DeleteRating(Guid ratingId)
    {
        await _service.DeleteRatingAsync(ratingId);
        return NoContent();
    }
}
