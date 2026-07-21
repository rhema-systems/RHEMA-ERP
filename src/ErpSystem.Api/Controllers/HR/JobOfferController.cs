using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/job-offers")]
[Authorize]
public class JobOfferController : ControllerBase
{
    private readonly IJobOfferService _service;
    private readonly IOfferLetterService _offerLetter;
    private readonly ICurrentUserService _currentUser;

    public JobOfferController(IJobOfferService service, IOfferLetterService offerLetter, ICurrentUserService currentUser)
    {
        _service = service;
        _offerLetter = offerLetter;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetAll()
        => Ok(await _service.GetAllSummaryAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobOfferDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{offerNumber}")]
    public async Task<ActionResult<JobOfferDto?>> GetByOfferNumber(string offerNumber)
        => Ok(await _service.GetByOfferNumberAsync(offerNumber));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<JobOfferDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithFullDetailsAsync(id));

    [HttpGet("application/{applicationId:guid}")]
    public async Task<ActionResult<JobOfferDto?>> GetByApplication(Guid applicationId)
        => Ok(await _service.GetByApplicationIdAsync(applicationId));

    /// <summary>
    /// Generates the formal offer-of-employment letter for internal preview / print-to-PDF. The
    /// letter is rendered from the HR-editable "OfferLetter" template enriched with the offer terms,
    /// job-description summary + duties, itemised salary breakdown, benefits and pre-employment
    /// conditions.
    /// </summary>
    [HttpGet("{id:guid}/letter")]
    public async Task<ActionResult<OfferLetterDto>> GetOfferLetter(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _offerLetter.GenerateAsync(id, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetByStatus(JobOfferStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("expiring")]
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetExpiring(
        [FromQuery] int daysAhead = 7)
        => Ok(await _service.GetExpiringOffersAsync(daysAhead));

    [HttpGet("prepared-by/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetByPreparedBy(Guid employeeId)
        => Ok(await _service.GetByPreparedByAsync(employeeId));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<JobOfferDto>> Create([FromBody] CreateJobOfferDto dto)
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
    public async Task<ActionResult<JobOfferDto>> Update(Guid id, [FromBody] UpdateJobOfferDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/submit-for-approval")]
    public async Task<IActionResult> SubmitForApproval(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.SubmitForApprovalAsync(id);
        return Ok(new { message = "Offer submitted for approval." });
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveJobOfferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ApproveAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer approved." });
    }

    [HttpPost("{id:guid}/reject-approval")]
    public async Task<IActionResult> RejectApproval(Guid id, [FromBody] RejectJobOfferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.OfferId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RejectApprovalAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer approval rejected." });
    }

    [HttpPost("{id:guid}/issue")]
    public async Task<IActionResult> Issue(Guid id, [FromBody] IssueJobOfferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.IssueAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer issued." });
    }

    [HttpPost("{id:guid}/record-response")]
    public async Task<IActionResult> RecordResponse(Guid id, [FromBody] RecordOfferResponseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RecordResponseAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer response recorded." });
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokeJobOfferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RevokeAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer revoked." });
    }

    [HttpPost("{id:guid}/accept-conditionally")]
    public async Task<ActionResult<JobOfferDto>> AcceptConditionally(Guid id, [FromBody] string? candidateResponseNotes = null)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var result = await _service.AcceptConditionallyAsync(id, employeeId.Value, candidateResponseNotes);
        return Ok(result);
    }

    [HttpPost("{id:guid}/revise")]
    public async Task<ActionResult<JobOfferDto>> Revise(Guid id, [FromBody] ReviseJobOfferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.OriginalOfferId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var revised = await _service.ReviseOfferAsync(dto, employeeId.Value);
        return Ok(revised);
    }

    // =========================================================================
    // BENEFITS — POSITION GRADE INTEGRATION
    // =========================================================================

    /// <summary>Preview benefits from the position grade without persisting them.</summary>
    [HttpGet("{id:guid}/suggest-benefits")]
    public async Task<ActionResult<IEnumerable<JobOfferBenefitDto>>> SuggestBenefits(
        Guid id, CancellationToken ct)
        => Ok(await _service.SuggestBenefitsFromPositionAsync(id, ct));

    /// <summary>Import position-grade benefits into the offer (deduped, persists to DB).</summary>
    [HttpPost("{id:guid}/import-benefits")]
    public async Task<ActionResult<IEnumerable<JobOfferBenefitDto>>> ImportBenefits(
        Guid id, CancellationToken ct)
    {
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId   == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        var imported = await _service.ImportBenefitsFromPositionAsync(id, tenantId.Value, employeeId.Value, ct);
        return Ok(imported);
    }

    // =========================================================================
    // NOTES
    // =========================================================================

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<JobOfferNoteDto>> AddNote(Guid id, [FromBody] CreateJobOfferNoteDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (id != dto.JobOfferId) return BadRequest("ID mismatch.");

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var note = await _service.AddNoteAsync(dto, tenantId.Value, employeeId.Value);
        return Ok(note);
    }

    [HttpGet("{id:guid}/notes")]
    public async Task<ActionResult<IEnumerable<JobOfferNoteDto>>> GetNotes(Guid id)
        => Ok(await _service.GetNotesAsync(id));

    // =========================================================================
    // FILE UPLOADS
    // =========================================================================

    /// <summary>Upload or replace the offer letter PDF/DOCX.</summary>
    [HttpPost("{id:guid}/upload-letter")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadLetter(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        await using var stream = file.OpenReadStream();
        var url = await _service.UploadOfferLetterAsync(id, stream, file.FileName, ct);
        return Ok(new { url });
    }

    /// <summary>Upload the signed offer letter returned by the candidate.</summary>
    [HttpPost("{id:guid}/upload-signed-letter")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadSignedLetter(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        await using var stream = file.OpenReadStream();
        var url = await _service.UploadSignedLetterAsync(id, stream, file.FileName, ct);
        return Ok(new { url });
    }
}
